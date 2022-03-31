using System;
using System.Runtime.InteropServices;
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
    }
}
