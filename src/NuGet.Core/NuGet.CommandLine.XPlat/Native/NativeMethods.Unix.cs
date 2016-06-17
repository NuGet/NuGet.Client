// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.CommandLine.XPlat
{
    internal static partial class NativeMethods
    {
        public static class Unix
        {
            /// <summary>
            /// Returns the uname (short for unix name) that has the 
            /// name, version and details about the current machine and OS running on it.
            /// </summary>
            public unsafe static string GetUname()
            {
                // Utsname shouldn't be larger than 2K
                var buf = stackalloc byte[2048];

                try
                {
                    if (uname((IntPtr)buf) == 0)
                    {
                        return Marshal.PtrToStringAnsi((IntPtr)buf);
                    }
                }
                catch (Exception ex)
                {
                    throw new PlatformNotSupportedException("Error reading Unix name", ex);
                }
                throw new PlatformNotSupportedException("Unknown error reading Unix name");
            }

            [DllImport("libc")]
            private static extern int uname(IntPtr utsname);
        }
    }
}