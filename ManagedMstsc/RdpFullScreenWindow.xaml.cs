using MSTSCLib;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// RDPFullScreenWindow2.xaml の相互作用ロジック
    /// </summary>
    public partial class RdpFullScreenWindow : Window
    {
        public AxMSTSCLib.AxMsRdpClient9NotSafeForScripting RdpClient { get; private set; }

        public IntPtr uiMainWindowHandle { get; private set; } = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int smIndex);

        const int SM_XVIRTUALSCREEN = 76;
        const int SM_YVIRTUALSCREEN = 77;
        const int SM_CXVIRTUALSCREEN = 78;
        const int SM_CYVIRTUALSCREEN = 79;

        private RdpConnectingWindow connectingWindow;

        public RdpFullScreenWindow()
        {
            InitializeComponent();

            RdpClient = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
            RdpClient.BeginInit();
            RdpClient.Name = "rdpClient";
            windowsFormsHost.Child = RdpClient;
            RdpClient.EndInit();

            StateChanged += RDPFullScreenWindow2_StateChanged;

            Closing += RDPFullScreenWindow2_Closing;
        }

        private void RDPFullScreenWindow2_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (connectingWindow != null)
            {
                connectingWindow.Close();
                connectingWindow = null;
            }
        }

        private void RDPFullScreenWindow2_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                Visibility = Visibility.Collapsed;
                WindowState = WindowState.Normal;
                RdpClient.FullScreen = true;
            }
        }

        public void Connect()
        {
            connectingWindow = new RdpConnectingWindow();
            connectingWindow.Show();

            RdpClient.AdvancedSettings9.AuthenticationLevel = 2;
            RdpClient.AdvancedSettings9.EnableCredSspSupport = true;
            RdpClient.AdvancedSettings9.RedirectDrives = false;
            RdpClient.AdvancedSettings9.RedirectPrinters = false;
            RdpClient.AdvancedSettings9.RedirectPrinters = false;
            RdpClient.AdvancedSettings9.RedirectSmartCards = false;

            //RdpClient.AdvancedSettings9.GrabFocusOnConnect = true;

            RdpClient.ColorDepth = 24; // int value can be 8, 15, 16, or 24

            IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)RdpClient.GetOcx();

#if true
            // これを指定しても、シングルモニターの場合は機能しない。
            // マルチモニターの場合、これを指定すると以下の挙動が発生する。
            // ・勝手に全画面モードで開始
            // ・DesktopWidthとDesktopHeightなどが同期する
            innerOcx.UseMultimon = true;

            // 本プロパティをfalseにしても、UseMultimon=trueかつマルチモニター環境では勝手に全画面になる。
            RdpClient.FullScreen = true;
#endif

            // マルチモニターでない場合は、自分で設定要
            // マルチモニターの場合でも、上書きされるのでこのままでよい
            // https://araramistudio.jimdo.com/2017/05/17/c-%E3%81%A7%E7%94%BB%E9%9D%A2%E3%81%AE%E8%A7%A3%E5%83%8F%E5%BA%A6%E3%82%92%E5%8F%96%E5%BE%97%E3%81%99%E3%82%8B/
            RdpClient.DesktopWidth = (int)SystemParameters.PrimaryScreenWidth;
            RdpClient.DesktopHeight = (int)SystemParameters.PrimaryScreenHeight;

#if false
            innerOcx.DisableConnectionBar = true;
#else
            RdpClient.AdvancedSettings9.ConnectionBarShowMinimizeButton = false;
            RdpClient.AdvancedSettings9.PinConnectionBar = false;
#endif

            // Windows キーなどのホットキーを AxMsRdpClient にルーティングする。
            //RdpClient.AdvancedSettings9.EnableWindowsKey = 1;
            //RdpClient.SecuredSettings3.KeyboardHookMode = 1;

            RdpClient.OnEnterFullScreenMode += _rdpClient_OnEnterFullScreenMode;
            RdpClient.OnLeaveFullScreenMode += RdpClient_OnLeaveFullScreenMode;

            RdpClient.OnDisconnected += RdpClient_OnDisconnected;

            RdpClient.OnConnecting += _rdpClient_OnConnecting;
            RdpClient.OnConnected += RdpClient_OnConnected;

            RdpClient.Connect();
        }

        private void RdpClient_OnLeaveFullScreenMode(object sender, EventArgs e)
        {
            if (Visibility == Visibility.Collapsed)
            {
                Visibility = Visibility.Visible;
            }
        }

        private void RdpClient_OnConnected(object sender, EventArgs e)
        {
            if (connectingWindow != null)
            {
                connectingWindow.Close();
                connectingWindow = null;
            }

            if (RdpClient.FullScreen == false)
            {
                Visibility = Visibility.Visible;
            }
        }

        public string DisconnectReason { get; private set; }

        private void RdpClient_OnDisconnected(object sender, AxMSTSCLib.IMsTscAxEvents_OnDisconnectedEvent e)
        {
            DisconnectReason = RdpClient.GetErrorDescription((uint)e.discReason, (uint)RdpClient.ExtendedDisconnectReason);

            Close();
        }

        private void _rdpClient_OnConnecting(object sender, EventArgs e)
        {
            if (uiMainWindowHandle == IntPtr.Zero)
            {
                uiMainWindowHandle = FindWindowEx(RdpClient.Handle, IntPtr.Zero, "UIMainClass", null);
            }
        }

        private void _rdpClient_OnEnterFullScreenMode(object sender, EventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }

            IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)RdpClient.GetOcx();

            if (innerOcx.UseMultimon == true)
            {
                // すべてのモニターを使用している場合、
                // このタイミングで(プライマリーモニタ上で)最大化状態のウインドウサイスを、すべてのモニター領域に拡大する。

                // NOTE: 単一モニター使用の最大化において、正しく親のモニターを特定しているか確認する。必ずプライマリになるかも。
                // TODO: RDP上のRDPで正しく座標が取れていない可能性がある。確認する。
                MoveWindow(uiMainWindowHandle,
                    GetSystemMetrics(SM_XVIRTUALSCREEN),
                    GetSystemMetrics(SM_YVIRTUALSCREEN),
                    GetSystemMetrics(SM_CXVIRTUALSCREEN),
                    GetSystemMetrics(SM_CYVIRTUALSCREEN),
                    true);
            }
        }
    }
}
