using System;
using System.IO;
using System.Net;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal static class XPlatUtility
    {
        public const string HelpOption = "-h|--help";
        public const string VerbosityOption = "-v|--verbosity <verbosity>";

        public static ISettings CreateDefaultSettings()
        {
            return Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new CommandLineXPlatMachineWideSetting());
        }

        public static LogLevel GetLogLevel(CommandOption verbosity)
        {
            LogLevel level;
            if (!Enum.TryParse(value: verbosity.Value(), ignoreCase: true, result: out level))
            {
                level = LogLevel.Information;
            }

            return level;
        }

        public static void SetUserAgent()
        {
            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet xplat"));
        }

        public static void SetConnectionLimit()
        {
#if !NETSTANDARDAPP1_5
            // Increase the maximum number of connections per server.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }
#endif
        }
    }
}
