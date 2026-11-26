using Newtonsoft.Json;
using SuperVisionObjectDesktop.Network.DataClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SuperVisionObjectDesktop.Devices
{
    public class DeviceHandler : IDisposable
    {
        private IDevice _device;
        private readonly CancellationTokenSource _cts = new();
        private Task _task;
        private bool _disposed;

        public DeviceHandler(IDevice device)
        {
            _device = device;
        }

        public void Start()
        {
            _task = Task.Run(() => Worker(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();
            _task?.Wait(TimeSpan.FromSeconds(5));
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            long measurment = 0;
            var handler = new HttpClientHandler
            {
                Proxy = null,                    // Отключаем прокси
                UseProxy = false,                // Не использовать прокси
                AutomaticDecompression = DecompressionMethods.None,
                PreAuthenticate = false,
                UseDefaultCredentials = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://{_device.IPAddress}/stream");
            request.Headers.Add("Accept", "text/event-stream");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Connection", "keep-alive");


            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);


                if (!response.IsSuccessStatusCode)
                {
                    OnError?.Invoke(this, new Exception($"Ошибка: {response.StatusCode} - {response.ReasonPhrase}"));
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                string? line;
                string eventType = "";
                string eventData = "";

                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (line == null) 
                        continue;

                    if (line.StartsWith("event:"))
                    {
                        eventType = line.Substring(6).Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        eventData = line.Substring(5).Trim();
                    }
                    else if (string.IsNullOrEmpty(line))
                    {
                        if (!string.IsNullOrEmpty(eventData))
                        {
                            measurment++;
                            ProcessSSEMessage(eventType, eventData, measurment);
                        }
                        eventType = "";
                        eventData = "";
                    }
                }
            }
            catch (TaskCanceledException ex1)
            {
                OnError?.Invoke(this, ex1);
            }
            catch (OperationCanceledException ex2)
            {
                return;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }


        void ProcessSSEMessage(string eventType, string jsonData, long timestamp)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<SensorData>(jsonData);

                if (data == null) return;

                if (data.Type == "sensor_data")
                {
                    _device.Notify(new SensorDataEventArgs()
                    {
                        Channels = (ushort)data.MatrixRows,
                        Count = (ushort)data.MatrixCols,
                        Timestamp = data.Timestamp > 0 ? data.Timestamp : timestamp,
                        Data = data.Matrix
                    });
                }
                else if (data.Type == "init")
                {

                }
                else
                {
                    //
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }


        public event EventHandler<Exception> OnError;

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
