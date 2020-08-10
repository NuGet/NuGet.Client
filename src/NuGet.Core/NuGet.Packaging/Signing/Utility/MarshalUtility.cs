// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.Packaging.Signing.Utility
{
    internal static class MarshalUtility
    {
        internal static T PtrToStructure<T>(IntPtr pointer)
        {
#if NET45
            return (T)Marshal.PtrToStructure(pointer, typeof(T));
#else
            return Marshal.PtrToStructure<T>(pointer);
#endif
        }

        internal static int SizeOf<T>()
        {
#if NET45
            return Marshal.SizeOf(typeof(T));
#else
            return Marshal.SizeOf<T>();
#endif
        }
    }
}
