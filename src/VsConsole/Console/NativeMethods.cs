using System;
using System.Runtime.InteropServices;

namespace NuGetConsole
{
    internal static class NativeMethods
    {

        // Size of VARIANTs in 32 bit systems
        public const int VariantSize = 16;

        [DllImport("Oleaut32.dll", PreserveSig = false)]
        public static extern void VariantClear(IntPtr var);

        [DllImport("user32.dll")]
        public static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetKeyboardLayout(int dwLayout);
    }
}
