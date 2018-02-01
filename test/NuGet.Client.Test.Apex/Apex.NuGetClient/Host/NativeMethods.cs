using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace Apex.NuGetClient.Host
{
    internal class NativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr hWnd, ArrayList lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("User32.DLL")]
        public static extern bool EnumWindows(EnumWindowsProc enumWindowsProc, IntPtr lParam);

        [DllImport("User32.DLL")]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc enumWindowsProc, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);
    }
}
