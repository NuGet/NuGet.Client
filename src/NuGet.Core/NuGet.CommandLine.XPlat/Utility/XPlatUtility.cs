// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

#if IS_CORECLR
using System.Runtime.InteropServices;
#endif

using NuGet.Common;
using NuGet.Configuration;
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
                machineWideSettings: new XPlatMachineWideSetting());
        }

        public static void ConfigureProtocol()
        {
            // Set connection limit
            NetworkProtocolUtility.SetConnectionLimit();

            // Set user agent string used for network calls
            SetUserAgent();
        }

        public static void SetUserAgent()
        {
#if IS_CORECLR
            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet xplat")
                .WithOSDescription(RuntimeInformation.OSDescription));
#else
            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet xplat"));
#endif
        }
    }
}
