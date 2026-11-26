using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SuperVisionObjectDesktop.UI
{
    public static class AppleColors
    {
        // iOS / macOS Light Grouped Palette (Apple Health)
        public static readonly Color WindowBg = Color.FromArgb(242, 242, 247);      // #F2F2F7 systemGroupedBackground
        public static readonly Color HeaderBg = Color.FromArgb(249, 249, 251);      // #F9F9FB translucent chrome
        public static readonly Color CardBg = Color.FromArgb(255, 255, 255);        // #FFFFFF primary card
        public static readonly Color CardSubtle = Color.FromArgb(247, 247, 250);    // #F7F7FA inner card / sidebar item
        public static readonly Color CardSelected = Color.FromArgb(234, 244, 255);  // #EAF4FF soft blue tint
        public static readonly Color PlotBg = Color.FromArgb(255, 255, 255);        // #FFFFFF crisp medical chart

        public static readonly Color Hairline = Color.FromArgb(229, 229, 234);      // #E5E5EA separator
        public static readonly Color SoftShadow = Color.FromArgb(222, 222, 230);    // Subtle 1px bottom depth

        public static readonly Color LabelPrimary = Color.FromArgb(28, 28, 30);     // #1C1C1E
        public static readonly Color LabelSecondary = Color.FromArgb(110, 110, 115);// #6E6E73
        public static readonly Color LabelTertiary = Color.FromArgb(142, 142, 147); // #8E8E93

        // Apple System Accents (Light Mode)
        public static readonly Color SystemBlue = Color.FromArgb(0, 122, 255);      // #007AFF
        public static readonly Color SystemBlueHover = Color.FromArgb(0, 104, 224);
        public static readonly Color SystemBluePressed = Color.FromArgb(0, 84, 186);

        public static readonly Color SystemGreen = Color.FromArgb(52, 199, 89);     // #34C759
        public static readonly Color SystemGreenHover = Color.FromArgb(40, 180, 76);
        public static readonly Color SystemGreenPressed = Color.FromArgb(30, 152, 60);

        public static readonly Color SystemPink = Color.FromArgb(255, 45, 85);      // #FF2D55 Apple Health Heart
        public static readonly Color SystemOrange = Color.FromArgb(255, 149, 0);    // #FF9500
        public static readonly Color SystemRed = Color.FromArgb(255, 59, 48);       // #FF3B30

        public static readonly Color SecondaryBtnBg = Color.FromArgb(229, 229, 234);
        public static readonly Color SecondaryBtnHover = Color.FromArgb(216, 216, 222);
        public static readonly Color SecondaryBtnPressed = Color.FromArgb(199, 199, 204);

        // Optical Typography (Segoe UI Variable + Cascadia Mono)
        public static Font DisplayFont(float size, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI Variable Display", size, style);

        public static Font TextFont(float size, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI Variable Text", size, style);

        public static Font SmallFont(float size, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI Variable Small", size, style);

        public static Font MonoFont(float size, FontStyle style = FontStyle.Regular)
            => new Font("Cascadia Mono", size, style);
    }

    public static class AppleNativeWindow
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void EnableLightTitleBar(Form form)
        {
            void Apply()
            {
                if (!form.IsHandleCreated) return;
                int dark = 0;
                _ = DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                int captionColor = ColorTranslator.ToWin32(AppleColors.HeaderBg);
                _ = DwmSetWindowAttribute(form.Handle, 35, ref captionColor, sizeof(int));
            }

            if (form.IsHandleCreated) Apply();
            form.HandleCreated += (s, e) => Apply();
        }
    }

    public static class AppleGraphics
    {
        public static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0.5f)
            {
                path.AddRectangle(rect);
                return path;
            }

            float d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class AppleCardPanel : Panel
    {
        public int CornerRadius { get; set; } = 16;
        public Color CardColor { get; set; } = AppleColors.CardBg;
        public Color BorderColor { get; set; } = AppleColors.Hairline;
        public Color OuterColor { get; set; } = AppleColors.WindowBg;
        public int BorderThickness { get; set; } = 1;
        public bool ShowSoftShadow { get; set; } = true;

        public AppleCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppleColors.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(OuterColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            float bottomExtra = ShowSoftShadow ? 2f : 0.5f;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f - bottomExtra);
            if (rect.Width <= 4 || rect.Height <= 4) return;

            if (ShowSoftShadow)
            {
                var shadowRect = new RectangleF(rect.X, rect.Y + 1.5f, rect.Width, rect.Height);
                using var shadowPath = AppleGraphics.CreateRoundedPath(shadowRect, CornerRadius);
                using var shadowBrush = new SolidBrush(AppleColors.SoftShadow);
                e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using var path = AppleGraphics.CreateRoundedPath(rect, CornerRadius);
            using var brush = new SolidBrush(CardColor);
            e.Graphics.FillPath(brush, path);

            using var pen = new Pen(BorderColor, BorderThickness);
            e.Graphics.DrawPath(pen, path);
        }
    }

    public class AppleButton : Control
    {
        private bool _hovered;
        private bool _pressed;

        public int CornerRadius { get; set; } = 10;
        public bool IsPill { get; set; } = false;
        public Color FillColor { get; set; } = AppleColors.SystemBlue;
        public Color HoverColor { get; set; } = AppleColors.SystemBlueHover;
        public Color PressedColor { get; set; } = AppleColors.SystemBluePressed;
        public Color DisabledColor { get; set; } = Color.FromArgb(209, 209, 214);

        public AppleButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppleColors.HeaderBg;
            ForeColor = Color.White;
            Font = AppleColors.TextFont(9.5F, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Size = new Size(140, 36);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppleColors.HeaderBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float inset = _pressed ? 1.5f : 0.5f;
            var rect = new RectangleF(inset, inset, Width - inset * 2 - 0.5f, Height - inset * 2 - 0.5f);
            if (rect.Width <= 2 || rect.Height <= 2) return;

            float radius = IsPill ? rect.Height / 2f : CornerRadius;
            Color bg = !Enabled
                ? DisabledColor
                : (_pressed ? PressedColor : (_hovered ? HoverColor : FillColor));

            using var path = AppleGraphics.CreateRoundedPath(rect, radius);
            using var brush = new SolidBrush(bg);
            e.Graphics.FillPath(brush, path);

            Color fg = Enabled ? ForeColor : Color.White;
            using var textBrush = new SolidBrush(fg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(Text, Font, textBrush, rect, sf);
        }
    }

    public class AppleToggleSwitch : Control
    {
        private bool _checked;
        private bool _pressed;
        private float _animPos;
        private readonly System.Windows.Forms.Timer _animTimer;

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                if (!IsHandleCreated)
                    _animPos = _checked ? 1f : 0f;
                else
                    _animTimer.Start();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public AppleToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppleColors.CardSubtle;
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                float target = _checked ? 1f : 0f;
                float diff = target - _animPos;
                if (Math.Abs(diff) < 0.02f)
                {
                    _animPos = target;
                    _animTimer.Stop();
                }
                else
                {
                    _animPos += diff * 0.32f;
                }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _animTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed)
            {
                _pressed = false;
                if (ClientRectangle.Contains(e.Location))
                    Checked = !Checked;
                else
                    Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppleColors.CardSubtle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var trackRect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            float trackRadius = trackRect.Height / 2f;

            // Off: #D1D1D6 -> On: #34C759
            int r = (int)(209 + (52 - 209) * _animPos);
            int g = (int)(209 + (199 - 209) * _animPos);
            int b = (int)(214 + (89 - 214) * _animPos);
            Color trackColor = Enabled ? Color.FromArgb(r, g, b) : Color.FromArgb(229, 229, 234);

            using (var trackPath = AppleGraphics.CreateRoundedPath(trackRect, trackRadius))
            using (var trackBrush = new SolidBrush(trackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            float thumbSize = trackRect.Height - 4f;
            float thumbWidth = _pressed ? thumbSize + 4f : thumbSize;
            float minX = trackRect.X + 2f;
            float maxX = trackRect.Right - 2f - thumbWidth;
            float thumbX = minX + (maxX - minX) * _animPos;
            var thumbRect = new RectangleF(thumbX, trackRect.Y + 2f, thumbWidth, thumbSize);

            using var thumbPath = AppleGraphics.CreateRoundedPath(thumbRect, thumbSize / 2f);
            using var thumbBrush = new SolidBrush(Color.White);
            using var thumbPen = new Pen(Color.FromArgb(30, 0, 0, 0), 1f);
            e.Graphics.FillPath(thumbBrush, thumbPath);
            e.Graphics.DrawPath(thumbPen, thumbPath);
        }
    }

    public class AppleBadgeLabel : Control
    {
        public Color BadgeColor { get; set; } = Color.FromArgb(232, 248, 236);
        public Color DotColor { get; set; } = AppleColors.SystemGreen;

        public AppleBadgeLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppleColors.CardSubtle;
            ForeColor = Color.FromArgb(26, 138, 56);
            Font = AppleColors.SmallFont(8.5F, FontStyle.Bold);
            Size = new Size(180, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppleColors.CardSubtle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = AppleGraphics.CreateRoundedPath(rect, rect.Height / 2f))
            using (var brush = new SolidBrush(BadgeColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            float dotSize = 7f;
            float dotY = (Height - dotSize) / 2f;
            using (var dotBrush = new SolidBrush(DotColor))
            {
                e.Graphics.FillEllipse(dotBrush, 10f, dotY, dotSize, dotSize);
            }

            var textRect = new RectangleF(22f, 0f, Width - 28f, Height);
            using var textBrush = new SolidBrush(ForeColor);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            e.Graphics.DrawString(Text, Font, textBrush, textRect, sf);
        }
    }

    /// <summary>
    /// Apple macOS-style segmented pill switcher synced with a hidden-header TabControl.
    /// </summary>
    public class AppleTabStrip : Control
    {
        private readonly TabControl _tabControl;
        private int _hoveredIndex = -1;
        private readonly List<RectangleF> _tabRects = new();

        public AppleTabStrip(TabControl tabControl)
        {
            _tabControl = tabControl;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppleColors.CardBg;
            Height = 36;
            Font = AppleColors.TextFont(9F, FontStyle.Bold);
            Cursor = Cursors.Hand;

            _tabControl.ControlAdded += (s, e) => Invalidate();
            _tabControl.ControlRemoved += (s, e) => Invalidate();
            _tabControl.SelectedIndexChanged += (s, e) => Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitTest(e.Location);
            if (idx != _hoveredIndex)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoveredIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int idx = HitTest(e.Location);
                if (idx >= 0 && idx < _tabControl.TabPages.Count)
                {
                    _tabControl.SelectedIndex = idx;
                    Invalidate();
                }
            }
            base.OnMouseDown(e);
        }

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _tabRects.Count; i++)
            {
                if (_tabRects[i].Contains(pt))
                    return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            _tabRects.Clear();

            int count = _tabControl.TabPages.Count;
            if (count == 0) return;

            float totalWidth = Math.Min(Width - 4f, Math.Max(190f, count * 175f));
            var pillTrack = new RectangleF(0f, 2f, totalWidth, Height - 4f);

            using (var trackPath = AppleGraphics.CreateRoundedPath(pillTrack, 10f))
            using (var trackBrush = new SolidBrush(Color.FromArgb(236, 236, 241)))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            float segWidth = (pillTrack.Width - 6f) / count;
            for (int i = 0; i < count; i++)
            {
                var segRect = new RectangleF(pillTrack.X + 3f + i * segWidth, pillTrack.Y + 3f, segWidth, pillTrack.Height - 6f);
                _tabRects.Add(segRect);

                bool selected = i == _tabControl.SelectedIndex;
                bool hovered = i == _hoveredIndex;

                if (selected)
                {
                    using var segPath = AppleGraphics.CreateRoundedPath(segRect, 8f);
                    using var segBrush = new SolidBrush(Color.White);
                    using var segBorder = new Pen(Color.FromArgb(224, 224, 230), 1f);
                    e.Graphics.FillPath(segBrush, segPath);
                    e.Graphics.DrawPath(segBorder, segPath);
                }
                else if (hovered)
                {
                    using var segPath = AppleGraphics.CreateRoundedPath(segRect, 8f);
                    using var segBrush = new SolidBrush(Color.FromArgb(245, 245, 248));
                    e.Graphics.FillPath(segBrush, segPath);
                }

                string title = string.IsNullOrWhiteSpace(_tabControl.TabPages[i].Text)
                    ? $"Устройство {i + 1}"
                    : _tabControl.TabPages[i].Text;

                Color fg = selected ? AppleColors.LabelPrimary : AppleColors.LabelSecondary;
                using var txtBrush = new SolidBrush(fg);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                e.Graphics.DrawString(title, Font, txtBrush, segRect, sf);
            }
        }
    }
}
