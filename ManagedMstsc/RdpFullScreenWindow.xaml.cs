using MSTSCLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// RDPFullScreenWindow2.xaml の相互作用ロジック
    /// </summary>
    public partial class RdpFullScreenWindow : Window
    {
        #region Settings for public

        public string Server { get; set; }

        public string UserName { get; set; }

        public string ClearTextPassword { get; set; }

        public bool FullScreen { get; set; } = true;

        public bool UseMultimon { get; set; } = false;

        public bool DisableConnectionBar { get; set; } = false;

        public int KeyboardHookMode { get; set; } = 2;

        #endregion

        #region Result

        public int DisconnectReason { get; private set; }

        public ExtendedDisconnectReasonCode ExtendedDisconnectReason { get; private set; }

        /// <summary>
        /// 切断理由を取得します。
        /// </summary>
        /// <remarks>
        public string DisconnectReasonString { get; private set; }

        #endregion

        #region PInvoke

        internal class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            internal static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

            //[DllImport("user32.dll", SetLastError = true)]
            //internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

            [DllImport("user32.dll")]
            internal static extern int GetSystemMetrics(int smIndex);

            internal const int SM_XVIRTUALSCREEN = 76;
            internal const int SM_YVIRTUALSCREEN = 77;
            internal const int SM_CXVIRTUALSCREEN = 78;
            internal const int SM_CYVIRTUALSCREEN = 79;

            internal const int SWP_NOSIZE = 1;
            internal const int SWP_NOMOVE = 2;

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);

            internal const int SB_BOTH = 3;
        }

        #endregion

        private Window _parent;

        private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting _rdpClient;

        public IntPtr _uiMainWindowHandle { get; private set; } = IntPtr.Zero;

        private RdpConnectingWindow _connectingWindow;

        public RdpFullScreenWindow()
        {
            InitializeComponent();

            _rdpClient = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
            _rdpClient.BeginInit();
            _rdpClient.Name = "rdpClient";
            windowsFormsHost.Child = _rdpClient;
            _rdpClient.EndInit();

            StateChanged += Window_StateChanged;

            Closing += Window_Closing;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_connectingWindow != null)
            {
                _connectingWindow.Close();
                _connectingWindow = null;
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

        public void Connect(Window parent = null)
        {
            _parent = parent;

            // TODO: 親のモニターの中心のほうがよいかも。現状はプライマリになる。
            _connectingWindow = new RdpConnectingWindow();
            _connectingWindow.Show();

            IMsRdpClientAdvancedSettings8 advancedSettings = _rdpClient.AdvancedSettings9;
            IMsRdpClientSecuredSettings2 securedSettings = _rdpClient.SecuredSettings3;
            IMsRdpClientTransportSettings4 transportSettings = _rdpClient.TransportSettings4;
            IMsRdpClientNonScriptable7 innerOcx = (IMsRdpClientNonScriptable7)_rdpClient.GetOcx();

            advancedSettings.AuthenticationLevel = 2;
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
            _rdpClient.UserName = UserName;
            advancedSettings.ClearTextPassword = ClearTextPassword;
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

            _rdpClient.OnEnterFullScreenMode += _rdpClient_OnEnterFullScreenMode;
            _rdpClient.OnLeaveFullScreenMode += RdpClient_OnLeaveFullScreenMode;

            _rdpClient.OnDisconnected += RdpClient_OnDisconnected;

            _rdpClient.OnConnecting += _rdpClient_OnConnecting;
            _rdpClient.OnConnected += RdpClient_OnConnected;

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
                DisconnectReasonString = $"接続を開始できませんでした。({ex.Message})";
                Close();
            }
        }

        private void RdpClient_OnRemoteDesktopSizeChange(object sender, AxMSTSCLib.IMsTscAxEvents_OnRemoteDesktopSizeChangeEvent e)
        {
            Debug.WriteLine($"OnRemoteDesktopSizeChange");
        }

        private void RdpClient_OnConfirmClose(object sender, AxMSTSCLib.IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            _rdpClient.Disconnect();
        }

        private void RdpClient_OnLeaveFullScreenMode(object sender, EventArgs e)
        {
            if (Visibility == Visibility.Collapsed)
            {
                Visibility = Visibility.Visible;
            }

            Debug.WriteLine($"OnLeaveFullScreenMode");
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

        private void RdpClient_OnDisconnected(object sender, AxMSTSCLib.IMsTscAxEvents_OnDisconnectedEvent e)
        {
            DisconnectReason = e.discReason;
            ExtendedDisconnectReason = _rdpClient.ExtendedDisconnectReason;
            DisconnectReasonString = _rdpClient.GetErrorDescription((uint)e.discReason, (uint)_rdpClient.ExtendedDisconnectReason);

            if (nextProcDelegate != null)
            {
                SetWindowProc(_uiMainWindowHandle, nextProcDelegate);
            }

            Close();
        }

        private void _rdpClient_OnConnecting(object sender, EventArgs e)
        {
            if (_uiMainWindowHandle == IntPtr.Zero)
            {
                _uiMainWindowHandle = NativeMethods.FindWindowEx(_rdpClient.Handle, IntPtr.Zero, "UIMainClass", null);
                wndProcDelegate = new WndProcDelegate(RdpHookProc);
                nextProcDelegate = SetWindowProc(_uiMainWindowHandle, wndProcDelegate);
            }
        }

        #region subclass function

        const int GWL_WNDPROC = -4;

        const int WM_MOVE = 0x0003;
        const int WM_SIZE = 0x0005;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(WndProcDelegate lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, Delegate lpfnEnum, IntPtr dwData);

        static WndProcDelegate wndProcDelegate;
        static WndProcDelegate nextProcDelegate;

        private static WndProcDelegate SetWindowProc(IntPtr hWnd, WndProcDelegate newWndProc)
        {
            IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
            IntPtr oldWndProcPtr;

            if (IntPtr.Size == 4)
                oldWndProcPtr = SetWindowLongPtr32(hWnd, GWL_WNDPROC, newWndProcPtr);
            else
                oldWndProcPtr = SetWindowLongPtr64(hWnd, GWL_WNDPROC, newWndProcPtr);

            return (WndProcDelegate)Marshal.GetDelegateForFunctionPointer(oldWndProcPtr, typeof(WndProcDelegate));
        }

        private List<Rect> GetMonitorRects()
        {
            List<Rect> monitorRects = new List<Rect>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                new MonitorEnumDelegate((IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    monitorRects.Add(new Rect(lprcMonitor.Left, lprcMonitor.Top, lprcMonitor.Right - lprcMonitor.Left, lprcMonitor.Bottom - lprcMonitor.Top));
                    return true;
                }), IntPtr.Zero);

            return monitorRects;
        }

        bool reEntry = false;

        public IntPtr RdpHookProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WM_MOVE)
            {
                int x = (short)((uint)lParam & 0xFFFF);
                int y = (short)(((uint)lParam >> 16) & 0xFFFF);

                Debug.WriteLine($"WM_MOVE {_rdpClient.Connected} {_rdpClient.FullScreen} {x},{y}");

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
                        if ((x != expectX || y != expectY) && reEntry == false)
                        {
                            reEntry = true;

                            CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);

                            Debug.WriteLine($"FIX on WM_MOVE START");
                            NativeMethods.ShowScrollBar(_uiMainWindowHandle, NativeMethods.SB_BOTH, false);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            Debug.WriteLine($"FIX on WM_MOVE END");

                            reEntry = false;
                            return IntPtr.Zero;
                        }
                    }
                    else if (Left == double.NaN)
                    {
                        // FIXME: 自身の座標が NaN で無い場合は、本処理不要。まだ自身の座標が定まっていないとき専用の処理。
                        //        と思ったが、この段階では定まっていた。そもそも自身の座標を親のウインドウにする必要あり。

                        // シングルでも全画面の場合に、所属モニターの選定をしてあげる必要あり
                        // _parent のスクリーン座標の中心点が所属するディスプレイに表示させる、見つからない場合は無処理
                        List<Rect> monitorRects = GetMonitorRects();

                        Rect parentRect = new Rect(_parent.Left, _parent.Top, _parent.Width, _parent.Height);
                        Point parentPoint = _parent.PointToScreen(new Point(parentRect.Right - parentRect.Left, parentRect.Bottom - parentRect.Top));

                        foreach (Rect monitorRect in monitorRects)
                        {
                            if (monitorRect.Contains(parentPoint) == true && reEntry == false)
                            {
                                reEntry = true;
                                Debug.WriteLine($"FIX on WM_MOVE(Single) START");
                                NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, (int)monitorRect.Left, (int)monitorRect.Top, 0, 0, NativeMethods.SWP_NOSIZE);
                                Debug.WriteLine($"FIX on WM_MOVE(Single) END");
                                reEntry = false;
                                return IntPtr.Zero;
                            }
                        }

                    }
                }
            }

            if (message == WM_SIZE)
            {
                int cx = (short)((uint)lParam & 0xFFFF);
                int cy = (short)(((uint)lParam >> 16) & 0xFFFF);

                Debug.WriteLine($"WM_SIZE {_rdpClient.Connected} {_rdpClient.FullScreen} {wParam} {cx},{cy}");

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
                        if ((cx != expectCX || cy != expectCY) && reEntry == false)
                        {
                            reEntry = true;

                            CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);

                            Debug.WriteLine($"FIX on WM_SIZE START");
                            NativeMethods.ShowScrollBar(_uiMainWindowHandle, NativeMethods.SB_BOTH, false);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            NativeMethods.SetWindowPos(_uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            Debug.WriteLine($"FIX on WM_SIZE END");

                            reEntry = false;

                            return IntPtr.Zero;
                        }
                    }
                    else
                    {
                        // シングルでも全画面の場合に、所属モニターの選定をしてあげる必要あり
                        List<Rect> monitorRects = GetMonitorRects();

                        // _parent のスクリーン座標の中心点が所属するディスプレイに表示させる、見つからない場合は無処理
                    }
                }
            }

            return CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);
        }

        #endregion

        private void _rdpClient_OnEnterFullScreenMode(object sender, EventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }

            Debug.WriteLine($"OnEnterFullScreenMode");
        }
    }
}
