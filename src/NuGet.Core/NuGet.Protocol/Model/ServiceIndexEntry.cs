// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;
using NuGet.Common;
using NuGet.Configuration;
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

        private PackageSource _packageSource;

        public ServiceIndexEntry(Uri uri, string type, SemanticVersion clientVersion, PackageSource packageSource) : this(uri, type, clientVersion)
        {
            _packageSource = packageSource;

            if (_packageSource != null)
            {
                // Telemetry for HTTPS sources that have an HTTP resource
                string source;
                bool isResourceHTTP = Uri.Scheme == Uri.UriSchemeHttp;
                bool isResourceHTTPs = Uri.Scheme == Uri.UriSchemeHttps;

                try
                {
                    source = _packageSource.SourceUri.ToString();
                }
                catch (ArgumentException)
                {
                    source = _packageSource.Source;
                }
                catch (UriFormatException)
                {
                    source = _packageSource.Source;
                }

                var telemetry = new ServiceIndexEntryTelemetry(source, _packageSource.IsHttp, _packageSource.IsHttps, isResourceHTTP, isResourceHTTPs, "ServiceIndexEntrySummary");
                TelemetryActivity.EmitTelemetryEvent(telemetry);
            }
        }

        public ServiceIndexEntry(Uri serviceUri, string serviceType, SemanticVersion clientVersion)
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

            Uri = serviceUri;
            Type = serviceType;
            ClientVersion = clientVersion;
        }

        private class ServiceIndexEntryTelemetry : TelemetryEvent
        {
            public ServiceIndexEntryTelemetry(string sourceUri, bool isHTTP, bool isHTTPS, bool HasAnHttpResource, bool HasAnHttpsResource, string eventName) : base(eventName)
            {
                this["SourceHash"] = HashPackageName(sourceUri);
                this["IsHTTP"] = HasAnHttpResource;
                this["IsHTTPS"] = HasAnHttpResource;
                this["HasAnHTTPResource"] = HasAnHttpResource;
                this["HasAnHTTPSResource"] = HasAnHttpResource;
            }
        }

        private static string HashPackageName(string packageName)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(packageName);
                byte[] hash = sha256.ComputeHash(bytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }
    }
}
