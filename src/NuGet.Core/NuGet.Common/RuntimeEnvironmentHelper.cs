using System;

namespace NuGet.Common
{
    public static class RuntimeEnvironmentHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

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
                var platform = (int)Environment.OSVersion.Platform;
                return platform == 6;
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
