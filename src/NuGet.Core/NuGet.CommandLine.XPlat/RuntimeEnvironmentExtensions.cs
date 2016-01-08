using System;
using Microsoft.Extensions.PlatformAbstractions;

namespace NuGet.CommandLine.XPlat
{
    internal static class RuntimeEnvironmentExtensions
    {
        public static string GetRuntimeOsName(this IRuntimeEnvironment env)
        {
            string os = env.OperatingSystem ?? string.Empty;
            string ver = env.OperatingSystemVersion ?? string.Empty;
            if (env.OperatingSystemPlatform == Platform.Windows)
            {
                os = "win";

                if (env.OperatingSystemVersion.StartsWith("6.1", StringComparison.Ordinal))
                {
                    ver = "7";
                }
                else if (env.OperatingSystemVersion.StartsWith("6.2", StringComparison.Ordinal))
                {
                    ver = "8";
                }
                else if (env.OperatingSystemVersion.StartsWith("6.3", StringComparison.Ordinal))
                {
                    ver = "81";
                }
                else if (env.OperatingSystemVersion.StartsWith("10.0", StringComparison.Ordinal))
                {
                    ver = "10";
                }

                return os + ver;
            }
            else if (env.OperatingSystemPlatform == Platform.Darwin)
            {
                os = "osx";
            }
            else
            {
                // Just use the lower-case full name of the OS as the RID OS and tack on the version number
                os = os.ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(ver))
            {
                os = os + "." + ver;
            }
            return os;
        }
    }
}
