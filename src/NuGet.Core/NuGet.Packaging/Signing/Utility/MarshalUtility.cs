using System;
using System.Runtime.InteropServices;

namespace NuGet.Packaging.Signing.Utility
{
    internal static class MarshalUtility
    {

        internal static T PtrToStructure<T>(IntPtr pointer)
        {
#if !NET45
            return Marshal.PtrToStructure<T>(pointer);
#else
            return (T)Marshal.PtrToStructure(pointer, typeof(T));
#endif
        }

        internal static int SizeOf<T>()
        {
#if !NET45
            return Marshal.SizeOf<T>();
#else
            return Marshal.SizeOf(typeof(T));
#endif
        }
    }
}
