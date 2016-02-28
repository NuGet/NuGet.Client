using System;
using System.Runtime.InteropServices;

namespace StandaloneConsole
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        [DllImport("user32.dll")]
        public static extern short VkKeyScan(char ch);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetKeyboardLayout(int dwLayout);
    }
}
