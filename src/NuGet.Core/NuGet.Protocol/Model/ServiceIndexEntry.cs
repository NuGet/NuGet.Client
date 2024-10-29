// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;
using NuGet.Protocol.Events;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// index.json entry for v3
    /// </summary>
    public class ServiceIndexEntry
    {
        /// <summary>
        /// Service Uri
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Service Type
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Client version
        /// </summary>
        public SemanticVersion ClientVersion { get; }

        internal ServiceIndexEntry(Uri serviceUri, string serviceType, SemanticVersion clientVersion, PackageSource packageSource)
        {
            if (serviceUri == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (clientVersion == null)
            {
                throw new ArgumentNullException(nameof(clientVersion));
            }

            if (packageSource != null)
            {
                var resourceIsHttp = serviceUri.Scheme == Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps;
                ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticServiceIndexEntryEvent(packageSource.Source, resourceIsHttp && packageSource.IsHttps));
            }

            Uri = serviceUri;
            Type = serviceType;
            ClientVersion = clientVersion;
        }

        public ServiceIndexEntry(Uri serviceUri, string serviceType, SemanticVersion clientVersion) : this(serviceUri, serviceType, clientVersion, null) { }
    }
}
