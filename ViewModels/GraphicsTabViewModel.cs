using SuperVisionObjectDesktop.Devices;
using SuperVisionObjectDesktop.ViewModels.GraphicsTabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SuperVisionObjectDesktop.ViewModels
{
    public class GraphicsTabViewModel
    {
        List<UserControl> _tabs;
        List<IDevice> _devices;
        TabControl _graphicsTabs;
        public GraphicsTabViewModel(TabControl graphicsTabs, List<IDevice> devices)
        {
            _tabs = new List<UserControl>();
            _devices = devices;
            _graphicsTabs = graphicsTabs;
            _graphicsTabs.TabPages.Clear();

            PrepareTabs();
        }

        private void PrepareTabs()
        {
            foreach (var device in _devices)
            {
                switch (device.SensorType)
                {
                    case "MAX30105":
                        UserControl tab = new MAX30105Tabs(device) { Dock = DockStyle.Fill };
                        _tabs.Add(tab);
                        var tabPage = new TabPage()
                        {
                            Text = device.Name,
                            BackColor = Color.White,
                            ForeColor = Color.FromArgb(28, 28, 30)
                        };
                        tabPage.Controls.Add(tab);
                        _graphicsTabs.TabPages.Add(tabPage);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
