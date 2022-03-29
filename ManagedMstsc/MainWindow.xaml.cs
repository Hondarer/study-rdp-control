using MSTSCLib;
using System;
using System.Diagnostics;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        RdpFullScreenWindow rdpWindow;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            rdpWindow = new RdpFullScreenWindow();
            rdpWindow.Closed += Window_Closed;
            Visibility = Visibility.Collapsed;

            #region 設定反映

            // TODO: クラスに切り出すか、rdpWindow のプロパティに用意すべき。

            rdpWindow.RdpClient.Server = server.Text;
            rdpWindow.RdpClient.UserName = user.Text;
            rdpWindow.RdpClient.AdvancedSettings9.ClearTextPassword = password.Password;
            rdpWindow.RdpClient.FullScreen = (bool)fullScreen.IsChecked;

            IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)rdpWindow.RdpClient.GetOcx();
            innerOcx.UseMultimon = (bool)useMultiMon.IsChecked;
            innerOcx.DisableConnectionBar = !(bool)useConnectionBar.IsChecked;

            if ((bool)hotkeyWhenNormalWindow.IsChecked == true)
            {
                rdpWindow.RdpClient.SecuredSettings3.KeyboardHookMode = 1; // default: 2
            }

            #endregion

            rdpWindow.Connect(this);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Debug.WriteLine($"rdpWindow.DisconnectReason: {rdpWindow.DisconnectReason}");

            rdpWindow.Closed -= Window_Closed;
            Visibility = Visibility.Visible;
            rdpWindow = null;
        }

        private void useMultiMon_Checked(object sender, RoutedEventArgs e)
        {
            fullScreen.IsEnabled = false;
            fullScreen.IsChecked = true;
        }

        private void useMultiMon_Unchecked(object sender, RoutedEventArgs e)
        {
            fullScreen.IsEnabled = true;
        }
    }
}
