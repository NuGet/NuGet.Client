﻿using System;

namespace NuGet.Configuration
{
    internal static class RuntimeEnvironmentHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        public static bool IsWindows {
            get
            {
#if DNXCORE50
                // This API does work on full framework but it requires a newer nuget client (RID aware)
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return true;
                }
                
                return false;
#else
                return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
            }
        }

        public static bool IsMono
        {
            get { return _isMono.Value; }
        }
    }
}
