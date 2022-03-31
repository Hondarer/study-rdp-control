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

            rdpWindow.Server = server.Text;
            rdpWindow.UserName = user.Text;
            rdpWindow.ClearTextPassword = password.Password;
            rdpWindow.FullScreen = (bool)fullScreen.IsChecked;
            rdpWindow.UseMultimon = (bool)useMultiMon.IsChecked;
            rdpWindow.DisableConnectionBar = !(bool)useConnectionBar.IsChecked;

            if ((bool)hotkeyWhenNormalWindow.IsChecked == true)
            {
                rdpWindow.KeyboardHookMode = 1;
            }

            #endregion

            rdpWindow.Connect(this);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Debug.WriteLine($"rdpWindow.DisconnectReason: {rdpWindow.DisconnectReason}");
            Console.WriteLine($"rdpWindow.DisconnectReason: {rdpWindow.DisconnectReason}");

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
