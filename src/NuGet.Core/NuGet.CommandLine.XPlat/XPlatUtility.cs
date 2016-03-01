using System;
using System.Net;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Common;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    public class XPlatUtility
    {
        internal const string HelpOption = "-h|--help";
        internal const string VerbosityOption = "-v|--verbosity <verbosity>";
        internal static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        internal static LogLevel GetLogLevel(CommandOption verbosity)
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
            UserAgent.UserAgentString
                = UserAgent.CreateUserAgentString(
                    $"NuGet xplat");
        }

        public static void SetConnectionLimit()
        {
#if !DNXCORE50
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
