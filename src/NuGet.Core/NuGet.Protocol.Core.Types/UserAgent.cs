﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public static class UserAgent
    {
        private static NuGetVersion _NuGetClientVersion;
        private static string _OSVersion;

        private const string UserAgentTemplate = "{0}/{1} ({2})";
        private const string UserAgentWithHostTemplate = "{0}/{1} ({2}, {3})";

        public const string NuGetClientName = "NuGet Client V3";

        /// <summary>
        /// To be set by NuGet Clients such as NuGet Extension for Visual Studio or nuget.exe
        /// </summary>
        public static string UserAgentString = string.Empty;

        /// <summary>
        /// Create user agent string with template of "{0}/{1} ({2})", where {0} is client name,
        /// {1} is NuGetClientVersion and {2} is OSVersion. {1} and {2} are automatically computed.
        /// </summary>
        /// <param name="client">Client name</param>
        /// <returns></returns>
        public static string CreateUserAgentString(string client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (NuGetTestMode.Enabled)
            {
                client = NuGetTestMode.NuGetTestClientName;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                UserAgentTemplate,
                client,
                NuGetClientVersion,
                OSVersion);
        }

        /// <summary>
        /// Create user agent string for operations on Visual Studio, with template of "{0}/{1} ({2}, {3})",
        /// where {0} is client name, {1} is NuGetClientVersion, {2} is OSVersion
        /// and {3} is visual studio Version and SKU. {1}, {2} are automatically computed. {3} is passed in.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static string CreateUserAgentStringForVisualStudio(string client, string visualStudioInfo)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (NuGetTestMode.Enabled)
            {
                client = NuGetTestMode.NuGetTestClientName;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                UserAgentWithHostTemplate,
                client,
                NuGetClientVersion, /* NuGet version */
                OSVersion, /* OS version */
                visualStudioInfo);  /* VS SKU + version */
        }

        /// <summary>
        /// Set user agent string on HttpClient to the static string.
        /// </summary>
        /// <param name="client">Http client</param>
        public static void SetUserAgent(HttpClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (!string.IsNullOrEmpty(UserAgentString))
            {
                client.DefaultRequestHeaders.Add("user-agent", UserAgentString);
            }
        }

        private static NuGetVersion NuGetClientVersion
        {
            get
            {
                if (_NuGetClientVersion == null)
                {
                    _NuGetClientVersion = GetNuGetVersion();
                }
                return _NuGetClientVersion;
            }
        }

        private static string OSVersion
        {
            get
            {
                if (_OSVersion == null)
                {
                    _OSVersion = OperatingSystemHelper.GetVersion();
                }
                return _OSVersion;
            }
        }

        private static NuGetVersion GetNuGetVersion()
        {
            Version attr = null;

#if !DNXCORE50
            attr = typeof(Repository).Assembly.GetName().Version;
#endif

            if (attr == null)
            {
                return new NuGetVersion(3, 0, 0, 0);
            }

            NuGetVersion version;
            NuGetVersion.TryParse(attr.ToString(), out version);

            return version;
        }

        private static class NuGetTestMode
        {
            private const string _testModeEnvironmentVariableName = "NuGetTestModeEnabled";
            public const string NuGetTestClientName = "NuGet Test Client";

            static NuGetTestMode()
            {
                // cached for the life-time of the app domain
                var testMode = Environment.GetEnvironmentVariable(_testModeEnvironmentVariableName);
                if (string.IsNullOrEmpty(testMode))
                {
                    Enabled = false;
                }

                bool isEnabled;
                Enabled = bool.TryParse(testMode, out isEnabled) && isEnabled;
            }

            public static bool Enabled { get; }
        }
    }
}
