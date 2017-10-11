// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.Common
{
    public static class RuntimeEnvironmentHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        [DllImport("libc")]
        static extern int uname(IntPtr buf);

        public static bool IsDev14 { get; set; }

        public static bool IsWindows
        {
            get
            {
#if IS_CORECLR
                // This API does work on full framework but it requires a newer nuget client (RID aware)
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return true;
                }

                return false;
#else
                var platform = (int)Environment.OSVersion.Platform;
                return (platform != 4) && (platform != 6) && (platform != 128);
#endif
            }
        }

        public static bool IsMono
        {
            get { return _isMono.Value; }
        }

        public static bool IsMacOSX
        {
            get
            {
#if IS_CORECLR
                // This API does work on full framework but it requires a newer nuget client (RID aware)
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    return true;
                }

                return false;
#else
                var buf = IntPtr.Zero;

                try
                {
                    buf = Marshal.AllocHGlobal(8192);

                    // This is a hacktastic way of getting sysname from uname ()
                    if (uname(buf) == 0)
                    {
                        var os = Marshal.PtrToStringAnsi(buf);

                        if (os == "Darwin")
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    if (buf != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                }

                return false;
#endif
            }
        }

        public static bool IsLinux
        {
            get
            {
#if IS_CORECLR
                // This API does work on full framework but it requires a newer nuget client (RID aware)
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    return true;
                }

                return false;
#else
                var platform = (int)Environment.OSVersion.Platform;
                return platform == 4;
#endif
            }
        }
    }
}