using System;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        RdpWindow rdpWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            rdpWindow = new RdpWindow();
            rdpWindow.Closed += RdpWindow_Closed;
            Hide();

            #region 設定反映

            rdpWindow.Server = server.Text;
            rdpWindow.UserName = user.Text;
            rdpWindow.Password = password.Password;
            rdpWindow.FullScreen = (bool)fullScreen.IsChecked;
            rdpWindow.UseMultimon = (bool)useMultiMon.IsChecked;
            rdpWindow.DisableConnectionBar = !(bool)useConnectionBar.IsChecked;

            if ((bool)hotkeyWhenNormalWindow.IsChecked == true)
            {
                rdpWindow.KeyboardHookMode = 1;
            }

            #endregion

            rdpWindow.Connect(new Rect(Left, Top, Width, Height));
        }

        private void RdpWindow_Closed(object sender, EventArgs e)
        {
            string result = JsonSerializer.Serialize(rdpWindow.Result, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });

            Debug.WriteLine($"{result}");
            Console.WriteLine($"{result}");

            rdpWindow.Closed -= RdpWindow_Closed;
            rdpWindow = null;

            // プログラムとして切断後は直ちに終わる場合
            //Close();

            Show();
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
