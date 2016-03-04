using System;

#if DNXCORE50
using System.Diagnostics;
using System.IO;
#endif

namespace NuGet.Protocol.Core.Types
{
    internal class OperatingSystemHelper
    {
#if !DNXCORE50
        public static string GetVersion()
        {
            return Environment.OSVersion.ToString();
        }
#else
        private static readonly Lazy<OperatingSystemInfo> _osInfo = new Lazy<OperatingSystemInfo>(GetOsDetails);

        public static string GetVersion()
        {
            return _osInfo.Value.ToString();
        }

        private static OperatingSystemInfo GetOsDetails()
        {
            var unameOutput = RunProgram("uname", "-s -m").Split(' ');

            var operatingSystem = unameOutput[0];
            string osVersion;

            if (operatingSystem == "Darwin")
            {
                // sw_vers returns versions in format "10.10{.4}" so ".4" needs to be removed if exists
                osVersion = RunProgram("sw_vers", "-productVersion");
                var firstDot = osVersion.IndexOf('.');
                var lastDot = osVersion.LastIndexOf('.');
                osVersion = lastDot >= 0 && firstDot != lastDot ? osVersion.Substring(0, lastDot) : osVersion;

                return new OperatingSystemInfo(operatingSystem, osVersion);
            }

            osVersion = string.Empty;
            var qualifiers = new[] { "ID=", "VERSION_ID=" };
            try
            {
                var osRelease = File.ReadAllLines("/etc/os-release");
                foreach (var qualifier in qualifiers)
                {
                    foreach (var line in osRelease)
                    {
                        if (line.StartsWith(qualifier))
                        {
                            if (osVersion.Length != 0)
                            {
                                osVersion += " ";
                            }
                            osVersion += line.Substring(qualifier.Length).Trim('"', '\'');
                        }
                    }
                }
            }
            catch
            {
            }

            if (osVersion.Length == 0)
            {
                // Could not determine OS version information. Defaulting to the empty string.
                return new OperatingSystemInfo(null, null);
            }

            return new OperatingSystemInfo(operatingSystem, osVersion);
        }

        private static string RunProgram(string name, string args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var process = Process.Start(processStartInfo);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Trim();
        }

        private class OperatingSystemInfo
        {
            public OperatingSystemInfo(string operatingSystem, string operatingSystemVersion)
            {
                OperatingSystem = operatingSystem;
                OperatingSystemVersion = operatingSystemVersion;
            }

            public string OperatingSystem { get; }

            public string OperatingSystemVersion { get; }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(OperatingSystem))
                {
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(OperatingSystemVersion))
                {
                    return OperatingSystem;
                }

                return $"{OperatingSystem} {OperatingSystemVersion}";
            }
        }
#endif
    }
}