using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;

namespace ManagedMstsc
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal class NativeMethods
        {
            internal const int ATTACH_PARENT_PROCESS = -1;

            [DllImport("Kernel32.dll")]
            internal static extern bool AttachConsole(int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr GetStdHandle(StandardHandle nStdHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool SetStdHandle(StandardHandle nStdHandle, IntPtr handle);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern FileType GetFileType(IntPtr handle);

            internal enum StandardHandle : uint
            {
                Input = unchecked((uint)-10),
                Output = unchecked((uint)-11),
                Error = unchecked((uint)-12)
            }

            internal enum FileType : uint
            {
                Unknown = 0x0000,
                Disk = 0x0001,
                Char = 0x0002,
                Pipe = 0x0003
            }
        }

        private static bool IsRedirected(IntPtr handle)
        {
            NativeMethods.FileType fileType = NativeMethods.GetFileType(handle);

            return (fileType == NativeMethods.FileType.Disk) || (fileType == NativeMethods.FileType.Pipe);
        }

        private static void AttachConsoleWithWpfApplication()
        {
            if (IsRedirected(NativeMethods.GetStdHandle(NativeMethods.StandardHandle.Output)))
            {
                _ = Console.Out;
            }

            bool errorRedirected = IsRedirected(NativeMethods.GetStdHandle(NativeMethods.StandardHandle.Error));
            if (errorRedirected == true)
            {
                _ = Console.Error;
            }
            else
            {
                NativeMethods.SetStdHandle(NativeMethods.StandardHandle.Error, NativeMethods.GetStdHandle(NativeMethods.StandardHandle.Output));
            }

            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        }

        static App()
        {
            AttachConsoleWithWpfApplication();
        }

        RdpWindow rdpWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                // 直接起動
                rdpWindow = new RdpWindow();

                foreach (string arg in e.Args)
                {
                    if (arg.StartsWith("/v:") == true)
                    {
                        rdpWindow.Server = arg.Substring(3);
                    }
                    if (arg.StartsWith("/u:") == true)
                    {
                        rdpWindow.UserName = arg.Substring(3);
                    }
                    if (arg.StartsWith("/p:") == true)
                    {
                        rdpWindow.Password = arg.Substring(3);
                    }
                }

                rdpWindow.Closed += RdpWindow_Closed;
                rdpWindow.Connect();
            }
            else
            {
                new MainWindow().Show();
            }

            base.OnStartup(e);
        }

        private void RdpWindow_Closed(object sender, EventArgs e)
        {
            string result = JsonSerializer.Serialize(rdpWindow.Result, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });

            Debug.WriteLine($"{result}");
            Console.WriteLine($"{result}");

            rdpWindow.Closed -= RdpWindow_Closed;
            rdpWindow = null;

            Shutdown();
        }
    }
}
