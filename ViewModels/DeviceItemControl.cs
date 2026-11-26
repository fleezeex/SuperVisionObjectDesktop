using SuperVisionObjectDesktop.Devices;
using SuperVisionObjectDesktop.UI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SuperVisionObjectDesktop.ViewModels
{
    public class DeviceItemControl : Panel
    {
        private IDevice _device;
        public IDevice Device => _device;

        private Label _titleLabel;
        private Label _ipLabel;
        private Label _sensorLabel;
        private Label _rssiLabel;
        private AppleButton _detailsButton;

        private bool _isHovered;
        private bool _isPressed;
        private bool _isResizing;

        public event EventHandler<IDevice>? ItemClicked;

        public DeviceItemControl(IDevice device)
        {
            _device = device;

            this.Margin = new Padding(0, 0, 0, 10);
            this.BackColor = AppleColors.CardSubtle;
            this.BorderStyle = BorderStyle.None;
            this.Padding = new Padding(14);
            this.Cursor = Cursors.Hand;
            this.Height = 122;
            this.MinimumSize = new Size(220, 122);
            this.DoubleBuffered = true;

            InitializeControls();
            UpdateData(device);

            this.ParentChanged += PersonItemControl_ParentChanged;
        }

        private Color CurrentCardColor()
        {
            if (_device.IsUsing)
                return AppleColors.CardSelected;
            if (_isPressed)
                return Color.FromArgb(232, 232, 238);
            if (_isHovered)
                return Color.FromArgb(241, 241, 246);
            return AppleColors.CardSubtle;
        }

        private void SyncChildColors()
        {
            Color bg = CurrentCardColor();
            this.BackColor = bg;
            if (_titleLabel != null) _titleLabel.BackColor = bg;
            if (_ipLabel != null) _ipLabel.BackColor = bg;
            if (_sensorLabel != null) _sensorLabel.BackColor = bg;
            if (_rssiLabel != null) _rssiLabel.BackColor = bg;
            if (_detailsButton != null) _detailsButton.BackColor = bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(AppleColors.CardBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f);
            if (rect.Width <= 4 || rect.Height <= 4) return;

            Color bg = CurrentCardColor();
            Color border = _device.IsUsing ? AppleColors.SystemBlue : AppleColors.Hairline;
            float borderW = _device.IsUsing ? 1.6f : 1f;

            using (var path = AppleGraphics.CreateRoundedPath(rect, 14f))
            using (var brush = new SolidBrush(bg))
            using (var pen = new Pen(border, borderW))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            // iOS selection circle
            float circleSize = 22f;
            float cx = Width - circleSize - 14f;
            float cy = 12f;
            var circleRect = new RectangleF(cx, cy, circleSize, circleSize);

            if (_device.IsUsing)
            {
                using var fill = new SolidBrush(AppleColors.SystemBlue);
                e.Graphics.FillEllipse(fill, circleRect);
                using var checkPen = new Pen(Color.White, 2.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                e.Graphics.DrawLines(checkPen, new[]
                {
                    new PointF(cx + 6f, cy + 11.5f),
                    new PointF(cx + 10f, cy + 15.5f),
                    new PointF(cx + 16.5f, cy + 7.5f)
                });
            }
            else
            {
                using var ringPen = new Pen(Color.FromArgb(199, 199, 204), 1.5f);
                e.Graphics.DrawEllipse(ringPen, circleRect);
            }

            // 4-bar RSSI indicator
            int activeBars = _device.RSSI >= -55 ? 4 : (_device.RSSI >= -67 ? 3 : (_device.RSSI >= -78 ? 2 : 1));
            Color barActiveColor = _device.RSSI >= -65
                ? AppleColors.SystemGreen
                : (_device.RSSI >= -78 ? AppleColors.SystemOrange : AppleColors.SystemRed);

            float barStartX = 14f;
            float barBaseY = Height - 15f;
            for (int i = 0; i < 4; i++)
            {
                float h = 4f + i * 3f;
                var barRect = new RectangleF(barStartX + i * 5f, barBaseY - h, 3f, h);
                using var barPath = AppleGraphics.CreateRoundedPath(barRect, 1.2f);
                using var barBrush = new SolidBrush(i < activeBars ? barActiveColor : Color.FromArgb(209, 209, 214));
                e.Graphics.FillPath(barBrush, barPath);
            }
        }

        private void InitializeControls()
        {
            _titleLabel = new Label
            {
                Location = new Point(14, 12),
                AutoSize = true,
                Font = AppleColors.DisplayFont(10.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary
            };

            _ipLabel = new Label
            {
                Location = new Point(14, 38),
                AutoSize = true,
                Font = AppleColors.MonoFont(8.8F, FontStyle.Regular),
                ForeColor = AppleColors.SystemBlue
            };

            _sensorLabel = new Label
            {
                Location = new Point(14, 60),
                AutoSize = true,
                Font = AppleColors.TextFont(8.5F, FontStyle.Regular),
                ForeColor = AppleColors.LabelSecondary
            };

            _rssiLabel = new Label
            {
                Location = new Point(38, 89),
                AutoSize = true,
                Font = AppleColors.SmallFont(8.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelSecondary
            };

            _detailsButton = new AppleButton
            {
                Text = "Подробнее",
                Size = new Size(88, 26),
                CornerRadius = 8,
                FillColor = AppleColors.SecondaryBtnBg,
                HoverColor = AppleColors.SecondaryBtnHover,
                PressedColor = AppleColors.SecondaryBtnPressed,
                Font = AppleColors.SmallFont(8.3F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary
            };
            _detailsButton.Click += ViewButton_Click;

            this.Controls.Add(_titleLabel);
            this.Controls.Add(_ipLabel);
            this.Controls.Add(_sensorLabel);
            this.Controls.Add(_rssiLabel);
            this.Controls.Add(_detailsButton);

            SyncChildColors();

            AttachInteraction(this);
            foreach (Control c in this.Controls)
            {
                if (c != _detailsButton)
                    AttachInteraction(c);
            }
        }

        private void AttachInteraction(Control ctrl)
        {
            ctrl.MouseEnter += (s, e) => { _isHovered = true; SyncChildColors(); Invalidate(); };
            ctrl.MouseLeave += (s, e) => { _isHovered = false; _isPressed = false; SyncChildColors(); Invalidate(); };
            ctrl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isPressed = true; SyncChildColors(); Invalidate(); } };
            ctrl.MouseUp += (s, e) => { if (_isPressed) { _isPressed = false; SyncChildColors(); Invalidate(); } };
            ctrl.Click += PersonItemControl_Click;
        }

        private void PersonItemControl_ParentChanged(object? sender, EventArgs e)
        {
            if (this.Parent != null)
            {
                this.Parent.Resize += Parent_Resize;
                UpdateSize();
            }
        }

        private void Parent_Resize(object? sender, EventArgs e)
        {
            UpdateSize();
        }

        private void UpdateSize()
        {
            if (_isResizing || this.Parent == null) return;

            _isResizing = true;
            try
            {
                int parentWidth = this.Parent.ClientSize.Width;
                int padding = this.Parent.Padding.Horizontal;
                int margin = this.Margin.Horizontal;
                int availableWidth = parentWidth - padding - margin;

                this.Width = Math.Max(this.MinimumSize.Width, availableWidth - 6);
                UpdateControlsLayout();
            }
            finally
            {
                _isResizing = false;
            }
        }

        private void UpdateControlsLayout()
        {
            if (_detailsButton == null) return;
            _detailsButton.Location = new Point(this.Width - _detailsButton.Width - 14, this.Height - _detailsButton.Height - 12);
        }

        public void UpdateData(IDevice device)
        {
            _device = device;

            _titleLabel.Text = _device.Name;
            _ipLabel.Text = $"{_device.IPAddress}";
            _sensorLabel.Text = $"{_device.SensorType}  ·  {_device.SampleFrequancy} Гц";
            _rssiLabel.Text = $"{_device.RSSI} dBm";

            SyncChildColors();
            Invalidate();
            UpdateControlsLayout();
        }

        private void PersonItemControl_Click(object? sender, EventArgs e)
        {
            OnItemClicked();
        }

        private void OnItemClicked()
        {
            _device.IsUsing = !_device.IsUsing;
            SyncChildColors();
            Invalidate();
            ItemClicked?.Invoke(this, _device);
        }

        private void ViewButton_Click(object? sender, EventArgs e)
        {
            string details = $"Устройство: {_device.Name}\n" +
                            $"MAC: {_device.Mac}\n" +
                            $"Тип: {_device.DeviceType}\n" +
                            $"Сенсор: {_device.SensorType}\n" +
                            $"IP: {_device.IPAddress}\n" +
                            $"Каналы / Частота: {_device.ChannelsCount} / {_device.SampleFrequancy} Гц\n" +
                            $"Прошивка: {_device.FirmwareVersion}\n" +
                            $"Сигнал Wi-Fi: {_device.RSSI} dBm";

            MessageBox.Show(details, "Информация об устройстве", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public bool IsSelected => _device.IsUsing;

        public void SetSelected(bool selected)
        {
            _device.IsUsing = selected;
            SyncChildColors();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_isResizing)
            {
                UpdateControlsLayout();
            }
        }
    }
}
