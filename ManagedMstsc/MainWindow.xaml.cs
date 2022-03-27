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

            rdpWindow.RdpClient.Server = server.Text;
            rdpWindow.RdpClient.UserName = user.Text;
            rdpWindow.RdpClient.AdvancedSettings9.ClearTextPassword = password.Password;

            rdpWindow.Connect();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Debug.WriteLine($"rdpWindow.DisconnectReason: {rdpWindow.DisconnectReason}");

            rdpWindow.Closed -= Window_Closed;
            Visibility = Visibility.Visible;
            rdpWindow = null;
        }
    }
}
