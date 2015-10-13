using System;
using System.IO;

namespace NuGet.Configuration
{
    internal static class RuntimeEnvironmentHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        // Checking the OS environment variable is most complete way to check this on dnxcore currently.
        private static Lazy<bool> _isWindows = new Lazy<bool>(() => 
            Environment.GetEnvironmentVariable("OS")?
            .Equals("WINDOWS_NT", StringComparison.OrdinalIgnoreCase) ?? false);

        public static bool IsWindows
        {
            get { return _isWindows.Value; }
        }

        public static bool IsMono
        {
            get { return _isMono.Value; }
        }
    }
}
