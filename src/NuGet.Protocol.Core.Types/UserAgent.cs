// Copyright (c) .NET Foundation. All rights reserved.
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

            return string.Format(
                CultureInfo.InvariantCulture,
                UserAgentWithHostTemplate,
                client,
                NuGetClientVersion, /* NuGet version */
                OSVersion, /* OS version */
                visualStudioInfo);  /* VS SKU + version */
        }

        /// <summary>
        /// Set user agent string on HttpClient.
        /// </summary>
        /// <param name="client">Http client</param>
        /// <param name="userAgent">User agent string</param>
        public static void SetUserAgent(HttpClient client, string userAgent)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                client.DefaultRequestHeaders.Add("user-agent", userAgent);
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
                    _OSVersion = GetOSVersion();
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

        private static string GetOSVersion()
        {
            string osVersion = string.Empty;

#if !DNXCORE50
            osVersion = Environment.OSVersion.ToString();
#endif
            // TODO: return OSVersion for DNXCORE50.
            return osVersion;
        }
    }
}
