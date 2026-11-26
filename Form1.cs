using SuperVisionObjectDesktop.Devices;
using SuperVisionObjectDesktop.UI;
using SuperVisionObjectDesktop.ViewModels;
using SuperVisionObjectDesktop.ViewModels.GraphicsTabs;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperVisionObjectDesktop
{
    public partial class Form1 : Form
    {
        DevicesListViewModel _devicesListViewModel;
        DevicesManager _devicesManager;
        GraphicsTabViewModel _graphicsTabViewModel;

        private Label _headerStatusLabel;
        private AppleButton _scanButton;
        private AppleButton _runButton;

        public Form1()
        {
            InitializeComponent();
            AppleNativeWindow.EnableLightTitleBar(this);
            ApplyAppleThemeAndLayout();

            tabControl1.TabPages.Clear();
            var startTab = new TabPage("Монитор ФПГ")
            {
                BackColor = AppleColors.CardBg,
                ForeColor = AppleColors.LabelPrimary
            };
            startTab.Controls.Add(new MAX30105Tabs(null) { Dock = DockStyle.Fill });
            tabControl1.TabPages.Add(startTab);

            _devicesListViewModel = new DevicesListViewModel(DevicesFlowLayout);

            LoadPicture.Visible = false;
            this.Load += Form1_Load;
            this.Resize += Form1_Resize;
            this.FormClosing += Form1_FormClosing;

            InitTasks();
        }

        private void ApplyAppleThemeAndLayout()
        {
            this.Text = "SuperVision Health — ФПГ Монитор";
            this.BackColor = AppleColors.WindowBg;
            this.ForeColor = AppleColors.LabelPrimary;
            this.Font = AppleColors.TextFont(9.5F, FontStyle.Regular);
            this.MinimumSize = new Size(1080, 680);
            this.StartPosition = FormStartPosition.CenterScreen;

            this.Controls.Clear();

            // 1. Apple Light Header Bar
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = AppleColors.HeaderBg,
                Padding = new Padding(22, 12, 22, 12)
            };
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(AppleColors.Hairline, 1);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var titleStack = new Panel
            {
                Dock = DockStyle.Left,
                Width = 440,
                BackColor = AppleColors.HeaderBg
            };

            var lblHeart = new Label
            {
                Text = "♥",
                Font = AppleColors.DisplayFont(17F, FontStyle.Bold),
                ForeColor = AppleColors.SystemPink,
                AutoSize = true,
                Location = new Point(0, 1),
                BackColor = AppleColors.HeaderBg
            };
            var lblTitle = new Label
            {
                Text = "SuperVision Health",
                Font = AppleColors.DisplayFont(14.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary,
                AutoSize = true,
                Location = new Point(28, 1),
                BackColor = AppleColors.HeaderBg
            };
            var lblSub = new Label
            {
                Text = "Мониторинг фотоплетизмограммы и пульса",
                Font = AppleColors.SmallFont(8.5F, FontStyle.Regular),
                ForeColor = AppleColors.LabelSecondary,
                AutoSize = true,
                Location = new Point(30, 27),
                BackColor = AppleColors.HeaderBg
            };
            titleStack.Controls.Add(lblHeart);
            titleStack.Controls.Add(lblTitle);
            titleStack.Controls.Add(lblSub);

            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                BackColor = AppleColors.HeaderBg,
                Padding = new Padding(0, 4, 0, 0)
            };

            _runButton = new AppleButton
            {
                Text = "Запустить поток",
                Size = new Size(156, 36),
                IsPill = true,
                FillColor = AppleColors.SystemGreen,
                HoverColor = AppleColors.SystemGreenHover,
                PressedColor = AppleColors.SystemGreenPressed,
                Margin = new Padding(10, 0, 0, 0)
            };
            _runButton.Click += button2_Click;

            _scanButton = new AppleButton
            {
                Text = "Найти устройства",
                Size = new Size(162, 36),
                IsPill = true,
                FillColor = AppleColors.SystemBlue,
                HoverColor = AppleColors.SystemBlueHover,
                PressedColor = AppleColors.SystemBluePressed,
                Margin = new Padding(10, 0, 0, 0)
            };
            _scanButton.Click += button1_Click;

            LoadPicture.Size = new Size(28, 28);
            LoadPicture.Margin = new Padding(8, 4, 4, 0);
            LoadPicture.BackColor = AppleColors.HeaderBg;

            _headerStatusLabel = new Label
            {
                Text = "",
                ForeColor = AppleColors.LabelSecondary,
                Font = AppleColors.TextFont(9F, FontStyle.Regular),
                AutoSize = true,
                BackColor = AppleColors.HeaderBg,
                Margin = new Padding(8, 9, 10, 0)
            };

            actionsPanel.Controls.Add(_runButton);
            actionsPanel.Controls.Add(_scanButton);
            actionsPanel.Controls.Add(LoadPicture);
            actionsPanel.Controls.Add(_headerStatusLabel);

            headerPanel.Controls.Add(actionsPanel);
            headerPanel.Controls.Add(titleStack);

            // 2. Main Grouped Canvas
            var bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                BackColor = AppleColors.WindowBg
            };

            // Правая белая карточка со списком устройств
            var sidebarCard = new AppleCardPanel
            {
                Dock = DockStyle.Right,
                Width = 340,
                OuterColor = AppleColors.WindowBg,
                CardColor = AppleColors.CardBg,
                Padding = new Padding(14, 14, 14, 14)
            };

            var sidebarHeader = new Label
            {
                Text = "Устройства",
                Dock = DockStyle.Top,
                Height = 28,
                Font = AppleColors.DisplayFont(12.5F, FontStyle.Bold),
                ForeColor = AppleColors.LabelPrimary,
                BackColor = AppleColors.CardBg,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var sidebarSub = new Label
            {
                Text = "Отметьте датчик для подключения",
                Dock = DockStyle.Top,
                Height = 20,
                Font = AppleColors.SmallFont(8.5F, FontStyle.Regular),
                ForeColor = AppleColors.LabelSecondary,
                BackColor = AppleColors.CardBg
            };

            DevicesFlowLayout.Dock = DockStyle.Fill;
            DevicesFlowLayout.BackColor = AppleColors.CardBg;
            DevicesFlowLayout.FlowDirection = FlowDirection.TopDown;
            DevicesFlowLayout.WrapContents = false;
            DevicesFlowLayout.AutoScroll = true;
            DevicesFlowLayout.Padding = new Padding(0, 10, 2, 4);

            sidebarCard.Controls.Add(DevicesFlowLayout);
            sidebarCard.Controls.Add(sidebarSub);
            sidebarCard.Controls.Add(sidebarHeader);

            var spacer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 16,
                BackColor = AppleColors.WindowBg
            };

            // Левая белая карточка с ФПГ
            var chartCard = new AppleCardPanel
            {
                Dock = DockStyle.Fill,
                OuterColor = AppleColors.WindowBg,
                CardColor = AppleColors.CardBg,
                Padding = new Padding(16, 14, 16, 16)
            };

            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Appearance = TabAppearance.FlatButtons;
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.ItemSize = new Size(0, 1);

            var tabStrip = new AppleTabStrip(tabControl1)
            {
                Dock = DockStyle.Top
            };
            var stripSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 12,
                BackColor = AppleColors.CardBg
            };

            chartCard.Controls.Add(tabControl1);
            chartCard.Controls.Add(stripSpacer);
            chartCard.Controls.Add(tabStrip);

            bodyPanel.Controls.Add(chartCard);
            bodyPanel.Controls.Add(spacer);
            bodyPanel.Controls.Add(sidebarCard);

            this.Controls.Add(bodyPanel);
            this.Controls.Add(headerPanel);
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _devicesManager?.Dispose();
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            foreach (Control control in DevicesFlowLayout.Controls)
            {
                if (control is DeviceItemControl item)
                {
                    item.Width = DevicesFlowLayout.ClientSize.Width - DevicesFlowLayout.Padding.Horizontal - 12;
                }
            }
        }

        private async void InitTasks()
        {
            await RefreshDevicesAsync();
        }

        private async void button1_Click(object? sender, EventArgs e)
        {
            await RefreshDevicesAsync();
        }

        private async Task RefreshDevicesAsync()
        {
            LoadPicture.Visible = true;
            _scanButton.Enabled = false;
            _headerStatusLabel.Text = "Поиск датчиков...";
            try
            {
                await _devicesListViewModel.InitComponents();
                int count = DevicesFlowLayout.Controls.Count;
                _headerStatusLabel.Text = count > 0
                    ? $"Найдено: {count}"
                    : "Устройства не найдены";
            }
            finally
            {
                LoadPicture.Visible = false;
                _scanButton.Enabled = true;
            }
        }

        private void button2_Click(object? sender, EventArgs e)
        {
            var devices = _devicesListViewModel.GetUsingDevices();
            if (devices.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одно устройство в списке справа.", "Устройство не выбрано", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _devicesManager?.Dispose();
            _graphicsTabViewModel = new GraphicsTabViewModel(tabControl1, devices);

            _devicesManager = new DevicesManager(devices);
            _devicesManager.RunProcess();
        }
    }
}
