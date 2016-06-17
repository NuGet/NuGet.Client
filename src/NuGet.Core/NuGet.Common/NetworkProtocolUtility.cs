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
    }
}
