using System;
using System.Collections.Generic;
using Microsoft.Extensions.PlatformAbstractions;

namespace NuGet.CommandLine.XPlat
{
    internal static class RuntimeEnvironmentExtensions
    {
        /// <summary>
        /// Infer the runtimes from the current environment.
        /// </summary>
        public static IEnumerable<string> GetDefaultRestoreRuntimes(string os, string runtimeOsName)
        {
            if (string.Equals(os, "Windows", StringComparison.Ordinal))
            {
                // Restore the minimum version of Windows. If the user wants other runtimes, they need to opt-in
                yield return "win7-x86";
                yield return "win7-x64";
            }
            else
            {
                // Core CLR only supports x64 on non-windows OSes.
                // Mono supports x86, for those scenarios the runtimes
                // will need to be passed in or added to project.json.
                yield return runtimeOsName + "-x64";
            }
        }

        public static string GetRuntimeOsName(this IRuntimeEnvironment env)
        {
            string os = env.OperatingSystem ?? string.Empty;
            string ver = env.OperatingSystemVersion ?? string.Empty;
            if (string.Equals(env.OperatingSystem, "Windows", StringComparison.OrdinalIgnoreCase))
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
            else if (string.Equals(env.OperatingSystem, "Darwin", StringComparison.OrdinalIgnoreCase) || string.Equals(env.OperatingSystem, "Mac OS X", StringComparison.OrdinalIgnoreCase))
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
