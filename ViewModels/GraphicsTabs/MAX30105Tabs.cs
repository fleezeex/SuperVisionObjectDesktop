using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using SuperVisionObjectDesktop.Devices;
using SuperVisionObjectDesktop.Network.DataClasses;
using SuperVisionObjectDesktop.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SuperVisionObjectDesktop.ViewModels.GraphicsTabs
{
    public class MAX30105Tabs : UserControl
    {
        private PlotView _plotView;
        private ListBox _logs;
        private SplitContainer _split;
        private PlotModel _model;

        private AppleToggleSwitch _ppgFilterSwitch;
        private AppleToggleSwitch _invertSwitch;
        private AppleButton _clearButton;

        private Label _bpmNumberLabel;
        private Label _bpmUnitLabel;
        private Label _irValueLabel;
        private AppleBadgeLabel _fingerBadge;

        private bool _filterInitialized;
        private double _dcBaseline;
        private double _acFiltered;
        private bool? _fingerPresent;
        private int _samplesSinceReset;

        private long? _lastTimestampMs;
        private double _effectiveFs = 100.0;
        private bool _fsCalibrated;
        private readonly List<double> _recentFs = new();
        private int _validBatchesSeen;

        private readonly IDevice? _dataSource;
        private readonly object _lock = new object();

        public MAX30105Tabs(IDevice? device)
        {
            _dataSource = device;
            this.BackColor = AppleColors.CardBg;
            this.ForeColor = AppleColors.LabelPrimary;
            this.Font = AppleColors.TextFont(9F, FontStyle.Regular);

            if (_dataSource != null)
                _dataSource.DataReceived += DataReceived;

            BuildLayout();
            InitializePlot();

            if (_dataSource == null)
                AppendLog("Выберите устройство справа и нажмите «Запустить поток».");
            else
                AppendLog($"Подключено: {_dataSource.Name} ({_dataSource.IPAddress})");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dataSource != null)
                    _dataSource.DataReceived -= DataReceived;
            }
            base.Dispose(disposing);
        }

        private void ResetPlotAndFilter()
        {
            lock (_lock)
            {
                _filterInitialized = false;
                _dcBaseline = 0;
                _acFiltered = 0;
                _samplesSinceReset = 0;
                if (_model?.Series.Count > 0 && _model.Series[0] is LineSeries series)
                {
                    series.Points.Clear();
                    _model.InvalidatePlot(true);
                }
            }
            _bpmNumberLabel.Text = "--";
        }

        private void DataReceived(object? sender, SensorDataEventArgs e)
        {
            if (e.Data == null || e.Data.Count == 0 || e.Data[0] == null || e.Data[0].Count == 0)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => DataReceived(sender, e)));
                return;
            }

            bool useFilter = _ppgFilterSwitch.Checked;
            bool invert = _invertSwitch.Checked;
            double lastRaw = 0;
            int? bpm = null;
            string? calibratedLog = null;

            double fs;
            lock (_lock)
            {
                var series = (LineSeries)_model.Series[0];

                if (_lastTimestampMs.HasValue)
                {
                    long dtMs = e.Timestamp - _lastTimestampMs.Value;
                    _lastTimestampMs = e.Timestamp;

                    if (dtMs > 0 && dtMs < 150)
                        return;

                    if (dtMs >= 150 && dtMs <= 10000)
                    {
                        _validBatchesSeen++;
                        if (_validBatchesSeen >= 2)
                        {
                            double batchFs = e.Data[0].Count * 1000.0 / dtMs;
                            _recentFs.Add(batchFs);
                            if (_recentFs.Count > 5)
                                _recentFs.RemoveAt(0);

                            var sorted = new List<double>(_recentFs);
                            sorted.Sort();
                            double medianFs = sorted[sorted.Count / 2];

                            if (!_fsCalibrated)
                            {
                                _effectiveFs = medianFs;
                                _fsCalibrated = true;
                                series.Points.Clear();
                                _filterInitialized = false;
                                _samplesSinceReset = 0;
                                calibratedLog = $"Частота потока: {medianFs:F1} Гц";
                            }
                            else
                            {
                                _effectiveFs = medianFs;
                            }
                        }
                    }
                }
                else
                {
                    _lastTimestampMs = e.Timestamp;
                    _effectiveFs = (_dataSource != null && _dataSource.SampleFrequancy > 0)
                        ? _dataSource.SampleFrequancy
                        : 100.0;
                }

                fs = _effectiveFs;
                double dt = 1.0 / fs;

                double baseX = series.Points.Count == 0
                    ? 0
                    : series.Points[^1].X + dt;

                double alphaDc = Math.Exp(-2.0 * Math.PI * 0.5 / fs);
                double alphaLp = Math.Exp(-2.0 * Math.PI * 5.0 / fs);
                const double jumpThreshold = 15000.0;

                for (int i = 0; i < e.Data[0].Count; i++)
                {
                    double raw = e.Data[0][i];
                    if (double.IsNaN(raw) || double.IsInfinity(raw))
                        continue;

                    lastRaw = raw;
                    double yPlot;

                    if (useFilter)
                    {
                        if (!_filterInitialized || Math.Abs(raw - _dcBaseline) > jumpThreshold)
                        {
                            _dcBaseline = raw;
                            _acFiltered = 0;
                            _filterInitialized = true;
                            _samplesSinceReset = 0;
                        }
                        else
                        {
                            _dcBaseline = alphaDc * _dcBaseline + (1.0 - alphaDc) * raw;
                            double ac = raw - _dcBaseline;
                            _acFiltered = alphaLp * _acFiltered + (1.0 - alphaLp) * ac;
                            _samplesSinceReset++;
                        }

                        yPlot = invert ? -_acFiltered : _acFiltered;
                    }
                    else
                    {
                        yPlot = raw;
                    }

                    series.Points.Add(new DataPoint(baseX + i * dt, yPlot));
                }

                int maxPoints = Math.Max(400, (int)Math.Round(fs * 8.0));
                int excess = series.Points.Count - maxPoints;
                if (excess > 0)
                    series.Points.RemoveRange(0, excess);

                _model.InvalidatePlot(true);

                if (useFilter && lastRaw > 20000 && _samplesSinceReset > (int)(fs * 2.5))
                {
                    bpm = EstimateBpm(series.Points, fs, invert);
                }
            }

            if (calibratedLog != null)
                AppendLog(calibratedLog);

            bool fingerNow = lastRaw > 20000;
            if (_fingerPresent != fingerNow)
            {
                _fingerPresent = fingerNow;
                AppendLog(fingerNow ? $"Палец обнаружен (IR={lastRaw:F0})" : $"Палец убран (IR={lastRaw:F0})");
            }

            _irValueLabel.Text = $"{lastRaw:N0}";
            if (fingerNow)
            {
                _fingerBadge.Text = $"Палец приложен · {fs:F0} Гц";
                _fingerBadge.ForeColor = Color.FromArgb(26, 138, 56);
                _fingerBadge.DotColor = AppleColors.SystemGreen;
                _fingerBadge.BadgeColor = Color.FromArgb(232, 248, 236);
                _fingerBadge.Invalidate();

                _bpmNumberLabel.Text = bpm.HasValue ? $"{bpm.Value}" : "•••";
            }
            else
            {
                _fingerBadge.Text = $"Приложите палец · {fs:F0} Гц";
                _fingerBadge.ForeColor = Color.FromArgb(184, 102, 0);
                _fingerBadge.DotColor = AppleColors.SystemOrange;
                _fingerBadge.BadgeColor = Color.FromArgb(255, 244, 224);
                _fingerBadge.Invalidate();

                _bpmNumberLabel.Text = "--";
            }
        }

        private static int? EstimateBpm(List<DataPoint> points, double fs, bool invert)
        {
            int count = Math.Min(points.Count, (int)(fs * 6));
            if (count < (int)(fs * 2.5))
                return null;

            int start = points.Count - count;
            var raw = new double[count];
            for (int i = 0; i < count; i++)
                raw[i] = invert ? points[start + i].Y : -points[start + i].Y;

            int halfWin = Math.Max(1, (int)Math.Round(fs * 0.06));
            var smoothed = new double[count];
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;

            for (int i = 0; i < count; i++)
            {
                int wStart = Math.Max(0, i - halfWin);
                int wEnd = Math.Min(count - 1, i + halfWin);
                double sum = 0;
                for (int j = wStart; j <= wEnd; j++)
                    sum += raw[j];
                double val = sum / (wEnd - wStart + 1);
                smoothed[i] = val;
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            double range = maxVal - minVal;
            if (range < 20.0)
                return null;

            double peakThreshold = minVal + range * 0.58;
            double valleyThreshold = minVal + range * 0.42;
            int minDistSamples = Math.Max(1, (int)(fs * 0.38));

            var peakIndices = new List<int>();
            bool seenValley = true;

            for (int i = 1; i < count - 1; i++)
            {
                if (smoothed[i] < valleyThreshold)
                    seenValley = true;

                if (smoothed[i] > peakThreshold && smoothed[i] >= smoothed[i - 1] && smoothed[i] > smoothed[i + 1])
                {
                    if (peakIndices.Count == 0)
                    {
                        peakIndices.Add(start + i);
                        seenValley = false;
                    }
                    else
                    {
                        int prevIdx = peakIndices[^1] - start;
                        if (seenValley && (i - prevIdx) >= minDistSamples)
                        {
                            peakIndices.Add(start + i);
                            seenValley = false;
                        }
                        else if (!seenValley && smoothed[i] > smoothed[prevIdx])
                        {
                            peakIndices[^1] = start + i;
                        }
                    }
                }
            }

            if (peakIndices.Count < 3)
                return null;

            var intervals = new List<double>();
            for (int i = 1; i < peakIndices.Count; i++)
            {
                double dtSec = points[peakIndices[i]].X - points[peakIndices[i - 1]].X;
                if (dtSec >= 0.38 && dtSec <= 1.7)
                    intervals.Add(dtSec);
            }

            if (intervals.Count == 0)
                return null;

            intervals.Sort();
            double medianSec = intervals[intervals.Count / 2];
            int bpm = (int)Math.Round(60.0 / medianSec);
            return (bpm >= 35 && bpm <= 200) ? bpm : null;
        }

        private static AppleCardPanel CreateElevatedCard(Padding? margin = null)
        {
            return new AppleCardPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                OuterColor = AppleColors.CardBg,
                CardColor = AppleColors.CardSubtle,
                BackColor = AppleColors.CardSubtle,
                BorderColor = AppleColors.Hairline,
                ShowSoftShadow = false,
                Margin = margin ?? new Padding(0, 0, 12, 0),
                Padding = new Padding(14, 10, 14, 10)
            };
        }

        private void BuildLayout()
        {
            var kpiGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = AppleColors.CardBg,
                Padding = new Padding(0, 0, 0, 12)
            };
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            // 1. Карточка Пульс
            var bpmCard = CreateElevatedCard();
            var bpmTitle = new Label
            {
                Text = "♥  ПУЛЬС",
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppleColors.SmallFont(8.2F, FontStyle.Bold),
                ForeColor = AppleColors.SystemPink,
                BackColor = AppleColors.CardSubtle
            };
            var bpmRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppleColors.CardSubtle,
                Padding = new Padding(0, 2, 0, 0)
            };
            _bpmNumberLabel = new Label
            {
                Text = "--",
                Font = AppleColors.DisplayFont(23F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary,
                AutoSize = true,
                BackColor = AppleColors.CardSubtle,
                Margin = new Padding(0, 0, 6, 0)
            };
            _bpmUnitLabel = new Label
            {
                Text = "уд/мин",
                Font = AppleColors.TextFont(10F, FontStyle.Bold),
                ForeColor = AppleColors.LabelSecondary,
                AutoSize = true,
                BackColor = AppleColors.CardSubtle,
                Margin = new Padding(0, 14, 0, 0)
            };
            bpmRow.Controls.Add(_bpmNumberLabel);
            bpmRow.Controls.Add(_bpmUnitLabel);
            bpmCard.Controls.Add(bpmRow);
            bpmCard.Controls.Add(bpmTitle);

            // 2. Карточка Сенсор и Статус
            var irCard = CreateElevatedCard();
            var irTitle = new Label
            {
                Text = "ОПТИЧЕСКИЙ СЕНСОР",
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppleColors.SmallFont(8.2F, FontStyle.Bold),
                ForeColor = AppleColors.LabelSecondary,
                BackColor = AppleColors.CardSubtle
            };
            _irValueLabel = new Label
            {
                Text = "—",
                Dock = DockStyle.Top,
                Height = 28,
                Font = AppleColors.MonoFont(13.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary,
                BackColor = AppleColors.CardSubtle
            };
            _fingerBadge = new AppleBadgeLabel
            {
                Text = _dataSource == null ? "Ожидание потока" : "Ожидание данных",
                ForeColor = AppleColors.LabelSecondary,
                DotColor = AppleColors.LabelTertiary,
                BadgeColor = Color.FromArgb(232, 232, 237),
                BackColor = AppleColors.CardSubtle,
                Dock = DockStyle.Bottom,
                Height = 23
            };
            irCard.Controls.Add(_fingerBadge);
            irCard.Controls.Add(_irValueLabel);
            irCard.Controls.Add(irTitle);

            // 3. Карточка Фильтров
            var ctrlCard = CreateElevatedCard(new Padding(0));
            var ctrlTitle = new Label
            {
                Text = "ОБРАБОТКА ФПГ",
                Dock = DockStyle.Top,
                Height = 18,
                Font = AppleColors.SmallFont(8.2F, FontStyle.Bold),
                ForeColor = AppleColors.LabelSecondary,
                BackColor = AppleColors.CardSubtle
            };
            var ctrlFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = AppleColors.CardSubtle,
                Padding = new Padding(0, 10, 0, 0)
            };

            _ppgFilterSwitch = new AppleToggleSwitch { Checked = true, Margin = new Padding(0, 2, 6, 0) };
            var lblFilter = new Label
            {
                Text = "Фильтр ФПГ",
                AutoSize = true,
                ForeColor = AppleColors.LabelPrimary,
                Font = AppleColors.TextFont(9F, FontStyle.Regular),
                BackColor = AppleColors.CardSubtle,
                Margin = new Padding(0, 5, 14, 0)
            };
            _ppgFilterSwitch.CheckedChanged += (s, e) =>
            {
                _invertSwitch.Enabled = _ppgFilterSwitch.Checked;
                ResetPlotAndFilter();
            };

            _invertSwitch = new AppleToggleSwitch { Checked = true, Margin = new Padding(0, 2, 6, 0) };
            var lblInvert = new Label
            {
                Text = "Инверсия",
                AutoSize = true,
                ForeColor = AppleColors.LabelPrimary,
                Font = AppleColors.TextFont(9F, FontStyle.Regular),
                BackColor = AppleColors.CardSubtle,
                Margin = new Padding(0, 5, 14, 0)
            };
            _invertSwitch.CheckedChanged += (s, e) => ResetPlotAndFilter();

            _clearButton = new AppleButton
            {
                Text = "Сброс",
                Size = new Size(74, 28),
                IsPill = true,
                FillColor = AppleColors.SecondaryBtnBg,
                HoverColor = AppleColors.SecondaryBtnHover,
                PressedColor = AppleColors.SecondaryBtnPressed,
                ForeColor = AppleColors.LabelPrimary,
                Font = AppleColors.SmallFont(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 0)
            };
            _clearButton.Click += (s, e) => ResetPlotAndFilter();

            ctrlFlow.Controls.Add(_ppgFilterSwitch);
            ctrlFlow.Controls.Add(lblFilter);
            ctrlFlow.Controls.Add(_invertSwitch);
            ctrlFlow.Controls.Add(lblInvert);
            ctrlFlow.Controls.Add(_clearButton);

            ctrlCard.Controls.Add(ctrlFlow);
            ctrlCard.Controls.Add(ctrlTitle);

            kpiGrid.Controls.Add(bpmCard, 0, 0);
            kpiGrid.Controls.Add(irCard, 1, 0);
            kpiGrid.Controls.Add(ctrlCard, 2, 0);

            // Нижняя область: График + Журнал
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                Panel1MinSize = 25,
                Panel2MinSize = 25,
                BackColor = AppleColors.CardBg,
                SplitterWidth = 12
            };

            var plotCard = new AppleCardPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                OuterColor = AppleColors.CardBg,
                CardColor = AppleColors.PlotBg,
                BackColor = AppleColors.PlotBg,
                BorderColor = AppleColors.Hairline,
                ShowSoftShadow = false,
                Padding = new Padding(8)
            };
            _plotView = new PlotView
            {
                Dock = DockStyle.Fill,
                BackColor = AppleColors.PlotBg
            };
            plotCard.Controls.Add(_plotView);

            var logCard = new AppleCardPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 14,
                OuterColor = AppleColors.CardBg,
                CardColor = AppleColors.CardSubtle,
                BackColor = AppleColors.CardSubtle,
                BorderColor = AppleColors.Hairline,
                ShowSoftShadow = false,
                Padding = new Padding(12)
            };
            var logHeader = new Label
            {
                Text = "Журнал событий",
                Dock = DockStyle.Top,
                Height = 26,
                Font = AppleColors.TextFont(9.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary,
                BackColor = AppleColors.CardSubtle
            };
            _logs = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = AppleColors.CardSubtle,
                ForeColor = AppleColors.LabelSecondary,
                BorderStyle = BorderStyle.None,
                Font = AppleColors.MonoFont(8.2F, FontStyle.Regular)
            };
            logCard.Controls.Add(_logs);
            logCard.Controls.Add(logHeader);

            _split.Panel1.Controls.Add(plotCard);
            _split.Panel2.Controls.Add(logCard);

            Controls.Add(_split);
            Controls.Add(kpiGrid);

            Load += (s, e) =>
            {
                try
                {
                    _split.Panel1MinSize = 200;
                    _split.Panel2MinSize = 160;
                    _split.SplitterDistance = (int)(Width * 0.74);
                }
                catch { }
            };
        }

        private void InitializePlot()
        {
            var bgPlot = OxyColor.FromRgb(255, 255, 255);
            var gridMajor = OxyColor.FromRgb(229, 229, 234);
            var gridMinor = OxyColor.FromRgb(242, 242, 247);
            var axisText = OxyColor.FromRgb(110, 110, 115);

            _model = new PlotModel
            {
                Title = "Фотоплетизмограмма",
                TitleColor = OxyColor.FromRgb(28, 28, 30),
                TitleFont = "Segoe UI Variable Display",
                TitleFontSize = 13,
                DefaultFont = "Segoe UI Variable Text",
                Background = bgPlot,
                PlotAreaBackground = bgPlot,
                PlotAreaBorderColor = OxyColor.FromRgb(229, 229, 234),
                TextColor = axisText
            };

            _model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Время, с",
                TitleColor = axisText,
                TextColor = axisText,
                TicklineColor = gridMajor,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = gridMajor,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = gridMinor
            });

            _model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Амплитуда",
                TitleColor = axisText,
                TextColor = axisText,
                TicklineColor = gridMajor,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = gridMajor,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = gridMinor
            });

            var series = new LineSeries
            {
                Title = "ФПГ",
                Color = OxyColor.FromRgb(255, 45, 85), // Apple Health Pink/Red
                StrokeThickness = 2.2
            };
            _model.Series.Add(series);

            _plotView.Model = _model;
        }

        public void AppendLog(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => AppendLog(message))); return; }
            _logs.Items.Add($"{DateTime.Now:HH:mm:ss}  {message}");
            _logs.TopIndex = _logs.Items.Count - 1;
        }

        public void ClearLogs() => _logs.Items.Clear();
    }
}
