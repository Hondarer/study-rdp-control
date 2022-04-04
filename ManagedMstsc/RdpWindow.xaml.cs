using MSTSCLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// RdpWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class RdpWindow : Window
    {
        #region Consts

        private const string WINDOW_TITLE = "RdpWindow";

        #endregion

        #region Settings for public

        public string Server { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public bool FullScreen { get; set; } = true;

        public bool UseMultimon { get; set; } = true;

        public bool DisableConnectionBar { get; set; } = true;

        public int KeyboardHookMode { get; set; } = 1; // default is 2

        #endregion

        #region Result

        public ResultEntity Result { get; private set; }

        #endregion

        #region Fields

        private Rect _parentRect;

        private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting _rdpClient;

        private IntPtr _uiMainWindowHandle;

        private RdpConnectingWindow _connectingWindow;

        bool _windowSizeChangedCalled = false;

        NativeMethods.WndProcDelegate _hookWndProcDelegate;

        NativeMethods.WndProcDelegate _originalWndProcDelegate;

        bool _reEntryHookProc = false;

        #endregion

        public RdpWindow()
        {
            InitializeComponent();

            _rdpClient = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
            _rdpClient.BeginInit();
            _rdpClient.Name = "rdpClient";
            windowsFormsHost.Child = _rdpClient;
            _rdpClient.EndInit();

            SizeChanged += Window_SizeChanged;
            StateChanged += Window_StateChanged;

            Closing += Window_Closing;

            Title = WINDOW_TITLE;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_windowSizeChangedCalled == true)
            {
                return;
            }

            Debug.WriteLine($"Window_SizeChanged");

            // FIXME: High-DPI未対応

            List<Rect> monitorRects = NativeMethods.GetMonitorRects().Values.ToList();

            Rect parentRect = new Rect(_parentRect.Left, _parentRect.Top, _parentRect.Width, _parentRect.Height);
            Point parentPoint = new Point(parentRect.Left + parentRect.Width / 2, parentRect.Top + parentRect.Height / 2);

            foreach (Rect monitorRect in monitorRects)
            {
                if (monitorRect.Contains(parentPoint) == true)
                {
                    Top = monitorRect.Height / 2 + monitorRect.Top - Height / 2;
                    Left = monitorRect.Width / 2 + monitorRect.Left - Width / 2;
                    break;
                }
            }

            _windowSizeChangedCalled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_rdpClient.Connected != 0)
            {
                // 接続中の場合は、切断する
                _rdpClient.Disconnect();

                // OnDisconnected イベント経由で再入されるのを待つ
                e.Cancel = true;
                return;
            }

            if (_connectingWindow != null)
            {
                _connectingWindow.Close();
                _connectingWindow = null;
            }

            if (_originalWndProcDelegate != null)
            {
                NativeMethods.SetWindowProc(_uiMainWindowHandle, _originalWndProcDelegate);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                Visibility = Visibility.Collapsed;
                WindowState = WindowState.Normal;
                _rdpClient.FullScreen = true;
            }
        }

        private void ConnectingWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // WindowStartupLocation="CenterScreen" では、プライマリー モニタの中心になるので
            // 親ウインドウの所属モニターの中心にするために、コードで補正している。

            // FIXME: High-DPI未対応

            List<Rect> monitorRects = NativeMethods.GetMonitorRects().Values.ToList();

            Rect parentRect = new Rect(_parentRect.Left, _parentRect.Top, _parentRect.Width, _parentRect.Height);
            Point parentPoint = new Point(parentRect.Left + parentRect.Width / 2, parentRect.Top + parentRect.Height / 2);

            foreach (Rect monitorRect in monitorRects)
            {
                if (monitorRect.Contains(parentPoint) == true)
                {
                    RdpConnectingWindow connectingWindow = sender as RdpConnectingWindow;

                    connectingWindow.Top = monitorRect.Height / 2 + monitorRect.Top - connectingWindow.Height / 2;
                    connectingWindow.Left = monitorRect.Width / 2 + monitorRect.Left - connectingWindow.Width / 2;
                    break;
                }
            }
        }

        public void Connect(Rect? parentRect = null)
        {
            if (parentRect != null)
            {
                _parentRect = (Rect)parentRect;
            }
            else
            {
                _parentRect = NativeMethods.GetMonitorRects().Values.FirstOrDefault();
            }

            IMsRdpClientAdvancedSettings8 advancedSettings = _rdpClient.AdvancedSettings9;
            IMsRdpClientSecuredSettings2 securedSettings = _rdpClient.SecuredSettings3;
            IMsRdpClientTransportSettings4 transportSettings = _rdpClient.TransportSettings4;
            IMsRdpClientNonScriptable7 innerOcx = (IMsRdpClientNonScriptable7)_rdpClient.GetOcx();

            // 2 にすると認証が必要(証明書エラーのダイアログ等が出るモード)になる。
            // 0 の場合は、ダイアログを必要としない。
            advancedSettings.AuthenticationLevel = 0;

            advancedSettings.EnableCredSspSupport = true;
            advancedSettings.RedirectDrives = false;
            advancedSettings.RedirectPrinters = false;
            advancedSettings.RedirectPrinters = false;
            advancedSettings.RedirectSmartCards = false;

            _rdpClient.ColorDepth = 32;

            // マルチモニターでない場合は、自分で設定要
            // マルチモニターの場合でも、上書きされるのでこのままでよい
            // https://araramistudio.jimdo.com/2017/05/17/c-%E3%81%A7%E7%94%BB%E9%9D%A2%E3%81%AE%E8%A7%A3%E5%83%8F%E5%BA%A6%E3%82%92%E5%8F%96%E5%BE%97%E3%81%99%E3%82%8B/
            _rdpClient.DesktopWidth = (int)SystemParameters.PrimaryScreenWidth;
            _rdpClient.DesktopHeight = (int)SystemParameters.PrimaryScreenHeight;

            _rdpClient.AdvancedSettings9.ConnectionBarShowMinimizeButton = false;
            _rdpClient.AdvancedSettings9.PinConnectionBar = false;

            _rdpClient.Server = Server;
            if (string.IsNullOrEmpty(UserName) != true)
            {
                _rdpClient.UserName = UserName;
            }
            if (string.IsNullOrEmpty(Password) != true)
            {
                advancedSettings.ClearTextPassword = Password;
            }
            _rdpClient.FullScreen = FullScreen;

            // これを指定しても、シングルモニターの場合は機能しない。
            // マルチモニターの場合、これを指定すると以下の挙動が発生する。
            // ・FullScreen プロパティにかかわらず、全画面モードで開始
            // ・DesktopWidth と DesktopHeight などが同期
            innerOcx.UseMultimon = UseMultimon;

            // 本プロパティを false にしても、UseMultimon == true かつマルチモニター環境では全画面になる。
            _rdpClient.FullScreen = FullScreen;

            innerOcx.DisableConnectionBar = DisableConnectionBar;

            securedSettings.KeyboardHookMode = KeyboardHookMode;

            // イベントをつける

            _rdpClient.OnEnterFullScreenMode += RdpClient_OnEnterFullScreenMode;
            _rdpClient.OnLeaveFullScreenMode += RdpClient_OnLeaveFullScreenMode;

            _rdpClient.OnDisconnected += RdpClient_OnDisconnected;

            _rdpClient.OnConnecting += RdpClient_OnConnecting;
            _rdpClient.OnConnected += RdpClient_OnConnected;

            _rdpClient.OnLoginComplete += RdpClient_OnLoginComplete;

            _rdpClient.OnConfirmClose += RdpClient_OnConfirmClose;

            _rdpClient.OnRemoteDesktopSizeChange += RdpClient_OnRemoteDesktopSizeChange;

            // 接続

            try
            {
                _rdpClient.Connect();
            }
            catch (Exception ex)
            {
                // サーバー名未指定などで通過
                Result = new ResultEntity($"接続を開始できませんでした。({ex.Message})");
                Close();
            }

            // 接続中ウインドウの表示
            _connectingWindow = new RdpConnectingWindow();
            _connectingWindow.SizeChanged += ConnectingWindow_SizeChanged;
            _connectingWindow.SetConnectingText("接続を要求しました...");
            _connectingWindow.Show();

            // タイトルの更新
            Title = $"{WINDOW_TITLE} - {_rdpClient.UserName}@{_rdpClient.Server}";
            innerOcx.ConnectionBarText = $"{_rdpClient.UserName}@{_rdpClient.Server}";
        }

        private void RdpClient_OnLoginComplete(object sender, EventArgs e)
        {
            Debug.WriteLine($"OnLoginComplete");

            // 接続完了時、ユーザー名とサーバー名を更新する
            // (保存された資格情報をもとに、ユーザーが決定する場合があるため)
            Title = $"{WINDOW_TITLE} - {_rdpClient.UserName}@{_rdpClient.Server}";
            IMsRdpClientNonScriptable7 innerOcx = (IMsRdpClientNonScriptable7)_rdpClient.GetOcx();
            innerOcx.ConnectionBarText = $"{_rdpClient.UserName}@{_rdpClient.Server}";
        }

        private void RdpClient_OnConnecting(object sender, EventArgs e)
        {
            Debug.WriteLine($"OnConnecting");
            _connectingWindow.SetConnectingText("接続しています...");

            if (_uiMainWindowHandle == IntPtr.Zero)
            {
                _uiMainWindowHandle = NativeMethods.FindWindowEx(_rdpClient.Handle, IntPtr.Zero, "UIMainClass", null);
                _hookWndProcDelegate = new NativeMethods.WndProcDelegate(RdpHookProc);
                _originalWndProcDelegate = NativeMethods.SetWindowProc(_uiMainWindowHandle, _hookWndProcDelegate);
            }
        }

        private void RdpClient_OnConnected(object sender, EventArgs e)
        {
            Debug.WriteLine($"OnConnected");

            if (_connectingWindow != null)
            {
                _connectingWindow.Close();
                _connectingWindow = null;
            }

            if (_rdpClient.FullScreen == false)
            {
                Visibility = Visibility.Visible;
            }
        }

        private void RdpClient_OnRemoteDesktopSizeChange(object sender, AxMSTSCLib.IMsTscAxEvents_OnRemoteDesktopSizeChangeEvent e)
        {
            Debug.WriteLine($"OnRemoteDesktopSizeChange width={e.width} height={e.height}");
        }

        private void RdpClient_OnEnterFullScreenMode(object sender, EventArgs e)
        {
            Debug.WriteLine($"OnEnterFullScreenMode");

            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void RdpClient_OnLeaveFullScreenMode(object sender, EventArgs e)
        {
            Debug.WriteLine($"OnLeaveFullScreenMode");

            if (Visibility == Visibility.Collapsed)
            {
                Visibility = Visibility.Visible;
            }
        }

        private void RdpClient_OnConfirmClose(object sender, AxMSTSCLib.IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            Debug.WriteLine($"OnConfirmClose pfAllowClose={e.pfAllowClose}");

            _rdpClient.Disconnect();
        }

        private void RdpClient_OnDisconnected(object sender, AxMSTSCLib.IMsTscAxEvents_OnDisconnectedEvent e)
        {
            Debug.WriteLine($"OnDisconnected discReason={e.discReason} ExtendedDisconnectReason={_rdpClient.ExtendedDisconnectReason}");

            string disconnectReasonString = _rdpClient.GetErrorDescription((uint)e.discReason, (uint)_rdpClient.ExtendedDisconnectReason);

            if (e.discReason == 1)
            {
                disconnectReasonString = "クライアント操作により切断しました。";
            }

            if (e.discReason == 2)
            {
                disconnectReasonString = "ユーザー操作により切断しました。";
            }

            if (e.discReason == 3)
            {
                disconnectReasonString = "サーバーにより切断しました。";
            }

            Result = new ResultEntity(
                disconnectReasonString,
                e.discReason,
                _rdpClient.ExtendedDisconnectReason);

            Close();
        }

        private IntPtr RdpHookProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == NativeMethods.WM_MOVE)
            {
                int x = (short)((uint)lParam & 0xFFFF);
                int y = (short)(((uint)lParam >> 16) & 0xFFFF);

                //Debug.WriteLine($"WM_MOVE {_rdpClient.Connected} {_rdpClient.FullScreen} {x},{y}");

                if (_rdpClient.FullScreen == true)
                {
                    IMsRdpClientNonScriptable7 innerOcx = (IMsRdpClientNonScriptable7)_rdpClient.GetOcx();
                    if (innerOcx.UseMultimon == true)
                    {
                        // マルチモニターの場合の処理
                        int expectX = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                        int expectY = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                        int expectCX = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
                        int expectCY = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
                        if ((x != expectX || y != expectY) && _reEntryHookProc == false)
                        {
                            _reEntryHookProc = true;

                            NativeMethods.CallWindowProc(_originalWndProcDelegate, hWnd, message, wParam, lParam);

                            //Debug.WriteLine($"FIX on WM_MOVE START");
                            NativeMethods.ShowScrollBar(_uiMainWindowHandle, NativeMethods.SB_BOTH, false);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            //Debug.WriteLine($"FIX on WM_MOVE END");

                            _reEntryHookProc = false;
                            return IntPtr.Zero;
                        }
                    }
                    else if (double.IsNaN(Left) == true)
                    {
                        // 自身の座標が NaN で無い場合は、本処理不要。まだ自身の座標が定まっていないとき専用の処理。

                        // FIXME: High-DPI未対応

                        // シングルでも全画面の場合に、所属モニターの選定をしてあげる必要あり
                        // _parent のスクリーン座標の中心点が所属するディスプレイに表示させる、見つからない場合は無処理
                        List<Rect> monitorRects = NativeMethods.GetMonitorRects().Values.ToList();

                        Rect parentRect = new Rect(_parentRect.Left, _parentRect.Top, _parentRect.Width, _parentRect.Height);
                        Point parentPoint = new Point(parentRect.Left + parentRect.Width / 2, parentRect.Top + parentRect.Height / 2);

                        foreach (Rect monitorRect in monitorRects)
                        {
                            if (monitorRect.Contains(parentPoint) == true && _reEntryHookProc == false)
                            {
                                _reEntryHookProc = true;
                                //Debug.WriteLine($"FIX on WM_MOVE(Single) START");
                                NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, (int)monitorRect.Left, (int)monitorRect.Top, 0, 0, NativeMethods.SWP_NOSIZE);
                                //Debug.WriteLine($"FIX on WM_MOVE(Single) END");
                                _reEntryHookProc = false;
                                return IntPtr.Zero;
                            }
                        }
                    }
                }
            }

            if (message == NativeMethods.WM_SIZE)
            {
                int cx = (short)((uint)lParam & 0xFFFF);
                int cy = (short)(((uint)lParam >> 16) & 0xFFFF);

                //Debug.WriteLine($"WM_SIZE {_rdpClient.Connected} {_rdpClient.FullScreen} {wParam} {cx},{cy}");

                if (_rdpClient.FullScreen == true)
                {
                    IMsRdpClientNonScriptable7 innerOcx = (IMsRdpClientNonScriptable7)_rdpClient.GetOcx();
                    if (innerOcx.UseMultimon == true)
                    {
                        // マルチモニターの場合の処理
                        int expectX = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                        int expectY = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                        int expectCX = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
                        int expectCY = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
                        if ((cx != expectCX || cy != expectCY) && _reEntryHookProc == false)
                        {
                            _reEntryHookProc = true;

                            NativeMethods.CallWindowProc(_originalWndProcDelegate, hWnd, message, wParam, lParam);

                            //Debug.WriteLine($"FIX on WM_SIZE START");
                            NativeMethods.ShowScrollBar(_uiMainWindowHandle, NativeMethods.SB_BOTH, false);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            //Debug.WriteLine($"FIX on WM_SIZE END");

                            _reEntryHookProc = false;

                            return IntPtr.Zero;
                        }
                    }
                }
            }

            return NativeMethods.CallWindowProc(_originalWndProcDelegate, hWnd, message, wParam, lParam);
        }
    }
}
