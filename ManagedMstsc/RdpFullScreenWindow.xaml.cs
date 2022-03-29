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

        private Window _parent;

        public void Connect(Window parent = null)
        {
            _parent = parent;

            connectingWindow = new RdpConnectingWindow();
            connectingWindow.Show();

            RdpClient.AdvancedSettings9.AuthenticationLevel = 2;
            RdpClient.AdvancedSettings9.EnableCredSspSupport = true;
            RdpClient.AdvancedSettings9.RedirectDrives = false;
            RdpClient.AdvancedSettings9.RedirectPrinters = false;
            RdpClient.AdvancedSettings9.RedirectPrinters = false;
            RdpClient.AdvancedSettings9.RedirectSmartCards = false;

            RdpClient.ColorDepth = 32;

            IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)RdpClient.GetOcx();

#if false // 外から与える
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

#if false // 外から与える
            innerOcx.DisableConnectionBar = true;
#endif

            RdpClient.AdvancedSettings9.ConnectionBarShowMinimizeButton = false;
            RdpClient.AdvancedSettings9.PinConnectionBar = false;

#if false // 外から与える
            // Windows キーなどのホットキーを AxMsRdpClient にルーティングする。
            RdpClient.SecuredSettings3.KeyboardHookMode = 1;
#endif

            RdpClient.OnEnterFullScreenMode += _rdpClient_OnEnterFullScreenMode;
            RdpClient.OnLeaveFullScreenMode += RdpClient_OnLeaveFullScreenMode;

            RdpClient.OnDisconnected += RdpClient_OnDisconnected;

            RdpClient.OnConnecting += _rdpClient_OnConnecting;
            RdpClient.OnConnected += RdpClient_OnConnected;

            RdpClient.OnConfirmClose += RdpClient_OnConfirmClose;

            RdpClient.OnRemoteDesktopSizeChange += RdpClient_OnRemoteDesktopSizeChange;

            RdpClient.Connect();
        }

        private void RdpClient_OnRemoteDesktopSizeChange(object sender, AxMSTSCLib.IMsTscAxEvents_OnRemoteDesktopSizeChangeEvent e)
        {
            Debug.WriteLine($"OnRemoteDesktopSizeChange");
        }

        private void RdpClient_OnConfirmClose(object sender, AxMSTSCLib.IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            RdpClient.Disconnect();
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

            if (nextProcDelegate != null)
            {
                SetWindowProc(uiMainWindowHandle, nextProcDelegate);
            }

            Close();
        }

        private void _rdpClient_OnConnecting(object sender, EventArgs e)
        {
            if (uiMainWindowHandle == IntPtr.Zero)
            {
                uiMainWindowHandle = FindWindowEx(RdpClient.Handle, IntPtr.Zero, "UIMainClass", null);
                wndProcDelegate = new WndProcDelegate(RdpHookProc);
                nextProcDelegate = SetWindowProc(uiMainWindowHandle, wndProcDelegate);
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

        private List<RECT> GetMonitorRects()
        {
            List<RECT> monitorRects = new List<RECT>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                new MonitorEnumDelegate((IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    monitorRects.Add(lprcMonitor);
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

                Debug.WriteLine($"WM_MOVE {RdpClient.Connected} {RdpClient.FullScreen} {x},{y}");

                if (RdpClient.FullScreen == true)
                {
                    IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)RdpClient.GetOcx();
                    if (innerOcx.UseMultimon == true)
                    {
                        // マルチモニターの場合の処理
                        int expectX = GetSystemMetrics(SM_XVIRTUALSCREEN);
                        int expectY = GetSystemMetrics(SM_YVIRTUALSCREEN);
                        int expectCX = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                        int expectCY = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                        if ((x != expectX || y != expectY) && reEntry == false)
                        {
                            reEntry = true;

                            CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);

                            Debug.WriteLine($"FIX on WM_MOVE START");
                            ShowScrollBar(uiMainWindowHandle, SB_BOTH, false);
                            SetWindowPos(uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            SetWindowPos(uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            Debug.WriteLine($"FIX on WM_MOVE END");

                            reEntry = false;
                            return IntPtr.Zero;
                        }
                    }
                    else
                    {
                        // シングルでも全画面の場合に、所属モニターの選定をしてあげる必要あり
                        List<RECT> monitorRects = GetMonitorRects();

                        // _parent のスクリーン座標の中心点が所属するディスプレイに表示させる、見つからない場合は無処理
                    }
                }
            }

            if (message == WM_SIZE)
            {
                int cx = (short)((uint)lParam & 0xFFFF);
                int cy = (short)(((uint)lParam >> 16) & 0xFFFF);

                Debug.WriteLine($"WM_SIZE {RdpClient.Connected} {RdpClient.FullScreen} {wParam} {cx},{cy}");

                if (RdpClient.FullScreen == true)
                {
                    IMsRdpClientNonScriptable5 innerOcx = (IMsRdpClientNonScriptable5)RdpClient.GetOcx();
                    if (innerOcx.UseMultimon == true)
                    {
                        // マルチモニターの場合の処理
                        int expectX = GetSystemMetrics(SM_XVIRTUALSCREEN);
                        int expectY = GetSystemMetrics(SM_YVIRTUALSCREEN);
                        int expectCX = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                        int expectCY = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                        if ((cx != expectCX || cy != expectCY) && reEntry == false)
                        {
                            reEntry = true;

                            CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);

                            Debug.WriteLine($"FIX on WM_SIZE START");
                            ShowScrollBar(uiMainWindowHandle, SB_BOTH, false);
                            SetWindowPos(uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            SetWindowPos(uiMainWindowHandle, 0, expectX, expectY, expectCX, expectCY, 0);
                            Debug.WriteLine($"FIX on WM_SIZE END");

                            reEntry = false;

                            return IntPtr.Zero;
                        }
                    }
                    else
                    {
                        // シングルでも全画面の場合に、所属モニターの選定をしてあげる必要あり
                        List<RECT> monitorRects = GetMonitorRects();

                        // _parent のスクリーン座標の中心点が所属するディスプレイに表示させる、見つからない場合は無処理
                    }
                }
            }

            return CallWindowProc(nextProcDelegate, hWnd, message, wParam, lParam);
        }

#endregion

        const int SWP_NOSIZE = 1;
        const int SWP_NOMOVE = 2;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);

        const int SB_BOTH = 3;

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
