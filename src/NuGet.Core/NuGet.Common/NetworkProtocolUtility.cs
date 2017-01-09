// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;

namespace NuGet.Common
{
    public static class NetworkProtocolUtility
    {
        /// <summary>
        /// This only has effect on .NET Framework (desktop). On .NET Core,
        /// <see cref="ServicePointManager"/> is not available. Additionally,
        /// no API is available to configure the supported SSL protocols.
        /// </summary>
        public static void ConfigureSupportedSslProtocols()
        {
#if !IS_CORECLR
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;
#endif
        }

        /// <summary>
        /// Set ServicePointManager.DefaultConnectionLimit
        /// </summary>
        public static void SetConnectionLimit()
        {
#if !IS_CORECLR
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
