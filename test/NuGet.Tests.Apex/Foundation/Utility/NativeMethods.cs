using System;
using System.Runtime.InteropServices;
using System.Security;

namespace NuGet.Tests.Foundation.Utility
{
    [SuppressUnmanagedCodeSecurity]
    public static class NativeMethods
    {
        /// <summary>
        /// Imports OLE32.dll:OleInitialize(int).
        /// </summary>
        [DllImport("ole32.dll", ExactSpelling = true, EntryPoint = "OleInitialize", SetLastError = true)]
        private static extern int IntOleInitialize(IntPtr value);

        /// <summary>
        /// Initializes OLE.
        /// </summary>
        /// <returns>The return value from initializing OLE.</returns>
        public static int OleInitialize()
        {
            return IntOleInitialize(IntPtr.Zero);
        }

        public const uint WM_CLOSE = 0x0010;
        public const uint WM_QUIT = 0x0012;
        public const int S_OK = 0x00000000;
        public const int S_FALSE = 0x00000001;

        public const int E_FAIL = unchecked((int)0x80004005);
        public const int E_POINTER = unchecked((int)0x80004003);
        public const int E_INVALIDARG = unchecked((int)0x80070057);
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_PENDING = unchecked((int)0x8000000A);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern int GetCurrentThreadId();

        [SecurityCritical, SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int PostThreadMessage(int id, uint msg, IntPtr wparam, IntPtr lparam);

        public static bool Succeeded(int hr) { return (hr >= 0); }
        public static bool Failed(int hr) { return (hr < 0); }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(IntPtr lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);
    }
}
