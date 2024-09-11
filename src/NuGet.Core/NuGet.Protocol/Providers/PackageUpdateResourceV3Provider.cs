// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageUpdateResourceV3Provider : ResourceProvider
    {
        public PackageUpdateResourceV3Provider()
            : base(
                  typeof(PackageUpdateResource),
                  nameof(PackageUpdateResourceV3Provider),
                  "PushCommandResourceV2Provider")
        { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            PackageUpdateResource packageUpdateResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var baseUrl = serviceIndex.GetServiceEntryUri(ServiceTypes.PackagePublish);

                if (baseUrl != null &&
                    source.PackageSource.IsHttps &&
                    baseUrl?.Scheme == Uri.UriSchemeHttp &&
                    baseUrl?.Scheme != Uri.UriSchemeHttps)
                {
                    // Telemetry for HTTPS sources that have an HTTP resource
                    var telemetry = new ServiceIndexEntryTelemetry(1, "RestorePackageSourceSummary");
                    TelemetryActivity.EmitTelemetryEvent(telemetry);
                }

                HttpSource httpSource = null;
                var sourceUri = baseUrl?.AbsoluteUri;
                if (!string.IsNullOrEmpty(sourceUri))
                {
                    if (!(new Uri(sourceUri)).IsFile)
                    {
                        var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                        httpSource = httpSourceResource.HttpSource;
                    }
                    packageUpdateResource = new PackageUpdateResource(sourceUri, httpSource);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.PackageServerEndpoint_NotSupported,
                        source));
                }
            }

            var result = new Tuple<bool, INuGetResource>(packageUpdateResource != null, packageUpdateResource);
            return result;
        }

        private class ServiceIndexEntryTelemetry : TelemetryEvent
        {
            public ServiceIndexEntryTelemetry(int NumSourceWithHttpResource, string eventName) : base(eventName)
            {
                this["NumPackageUpdateResourceWithHTTPSSource"] = NumSourceWithHttpResource;
            }
        }
    }
}
