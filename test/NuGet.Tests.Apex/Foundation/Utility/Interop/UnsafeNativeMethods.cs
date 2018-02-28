using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace NuGet.Tests.Foundation.Utility.Interop
{
    [SuppressUnmanagedCodeSecurity]
    internal static class UnsafeNativeMethods
    {
        internal const uint MB_ICONERROR = 0x00000010;
        internal const uint MB_ABORTRETRYIGNORE = 0x00000002;
        internal const uint MB_TASKMODAL = 0x00002000;

        internal const int IDABORT = 3;
        internal const int IDRETRY = 4;
        internal const int IDIGNORE = 5;

        internal const int WM_WINDOWPOSCHANGING = 0x0046;

        internal const int GW_OWNER = 4;

        internal const int GWL_EXSTYLE = -20;
        internal const int GWL_WNDPROC = -4;
        internal const int WS_EX_TOPMOST = 0x00000008;

        internal static readonly IntPtr HWND_NOTOPMOST = (IntPtr)(-2);
        internal static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);
        internal const int SWP_NOMOVE = 2;
        internal const int SWP_NOSIZE = 1;
        internal const int SWP_NOZORDER = 0x0004;
        internal const int SWP_NOACTIVATE = 0x10;

        internal const int S_OK = 0;
        internal const int STRSAFE_E_INSUFFICIENT_BUFFER = -2147024774; // 0x8007007A;

        internal delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "This is being used explicitly for Low Memory situations")]
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
        internal static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public int x;
            public int y;
            public Win32Point(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetShortPathNameW")]
        internal static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);

        /// <summary>
        /// Gets the full path name, resolving against the current working directory.  It does evaluate relative segments (".." and ".").
        /// It does not validate the path format being correct or existence of files.  Note that this does not have the normal path
        /// length limitation (MAX_PATH).
        /// </summary>
        /// <param name="lpFilePart">Should be IntPtr.Zero. If you want to get the file part you should create a new overload for this DllImport.</param>
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetFullPathName(string lpFileName, uint nBufferLength, StringBuilder lpBuffer, IntPtr lpFilePart);

        // Mouse methods
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        // GetKeyState can be removed when devdiv2:238083 is fixed.
        [DllImport("user32.dll", SetLastError = true)]
        public static extern System.UInt16 GetKeyState(int nVirtKey);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateDCW")]
        internal static extern IntPtr CreateDC(string strDriver, string strDevice, string strOutput, IntPtr pData);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        internal static extern int GetPixel(IntPtr hdc, int x, int y);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("mscoree.dll", CharSet = CharSet.Unicode)]
        internal extern static int GetFileVersion(string fileName, StringBuilder fileVersionString, int bufferSize, out int bufferLength);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("dwmapi.dll", PreserveSig = false, EntryPoint = "DwmIsCompositionEnabled")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternalDwmIsCompositionEnabled();

        private static bool dwmExists = true;
        internal static bool DwmIsCompositionEnabled()
        {
            try
            {
                return dwmExists && InternalDwmIsCompositionEnabled();
            }
            catch (DllNotFoundException)
            {
                // Unable to load DLL 'dwmapi.dll': The specified module could not be found. (Exception from HRESULT: 0x8007007E)
                return (dwmExists = false);
            }
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        internal static Win32Point GetCursorPosition()
        {
            Win32Point pt = new Win32Point();
            UnsafeNativeMethods.GetCursorPos(ref pt);
            return pt;
        }


        internal static class Ole32
        {
            [DllImport("ole32.dll")]
            public static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

            public static class BindContextStringKeys
            {
                public static string STR_PARSE_PREFER_FOLDER_BROWSING { get { return "Parse Prefer Folder Browsing"; } }
            }
        }

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern int GetCurrentThreadId();

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll")]
        internal static extern bool EnumThreadWindows(int dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowEnabled(IntPtr hWnd);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW", SetLastError = true)]
        private static extern int GetClassName(IntPtr hwnd, [Out]System.Text.StringBuilder className, int maxCount);

        internal static string GetClassName(IntPtr hwnd)
        {
            System.Text.StringBuilder className = new System.Text.StringBuilder(128);
            if (GetClassName(hwnd, className, className.Capacity) == 0)
            {
                return null;
            }
            return className.ToString();
        }

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

        internal static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
            }
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }

        internal static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, WndProc wndProc)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLongPtr32(hWnd, nIndex, wndProc);
            }
            return SetWindowLongPtr64(hWnd, nIndex, wndProc);
        }

        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")]
        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, WndProc wndProc);

        [SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")]
        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, WndProc wndProc);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int GetWindowLong(IntPtr handle, int index);

        internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4)
            {
                return GetWindowLongPtr32(hWnd, nIndex);
            }
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")]
        [DllImport("User32", CharSet = CharSet.Auto, EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("User32", CharSet = CharSet.Auto)]
        internal static extern IntPtr CallWindowProc(IntPtr pfnWndProc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern int GetWindowThreadProcessId(IntPtr hwnd, out int lpdwProcessId);

        [DllImport("User32", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetWindow(IntPtr hwnd, int cmd);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr handle, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetCapture();

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
