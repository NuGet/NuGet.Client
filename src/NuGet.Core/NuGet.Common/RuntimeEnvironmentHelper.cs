// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    public static class RuntimeEnvironmentHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        private static Lazy<bool> _isWindows = new Lazy<bool>(() => GetIsWindows());

        private static Lazy<bool> _IsMacOSX = new Lazy<bool>(() => GetIsMacOSX());

        private static Lazy<bool> _IsLinux = new Lazy<bool>(() => GetIsLinux());

        public static bool IsDev14 { get; set; }

        public static bool IsWindows
        {
            get => _isWindows.Value;
        }

        private static bool GetIsWindows()
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

        public static bool IsMono
        {
            get { return _isMono.Value; }
        }

        public static bool IsMacOSX
        {
            get => _IsMacOSX.Value;
        }

        private static bool GetIsMacOSX()
        {
#if IS_CORECLR
            // This API does work on full framework but it requires a newer nuget client (RID aware)
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return true;
            }

            return false;
#else
            var platform = (int)Environment.OSVersion.Platform;
            return platform == 6;
#endif
        }

        public static bool IsLinux
        {
            get => _IsLinux.Value;
        }

        private static bool GetIsLinux()
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