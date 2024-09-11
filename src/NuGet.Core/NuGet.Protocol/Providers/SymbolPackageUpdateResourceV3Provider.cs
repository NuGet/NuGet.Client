// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class SymbolPackageUpdateResourceV3Provider : ResourceProvider
    {
        public SymbolPackageUpdateResourceV3Provider()
            : base(typeof(SymbolPackageUpdateResourceV3),
                  nameof(SymbolPackageUpdateResourceV3),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var baseUrl = serviceIndex.GetServiceEntryUri(ServiceTypes.SymbolPackagePublish);

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
                    symbolPackageUpdateResource = new SymbolPackageUpdateResourceV3(sourceUri, httpSource);
                }
            }

            var result = new Tuple<bool, INuGetResource>(symbolPackageUpdateResource != null, symbolPackageUpdateResource);
            return result;
        }

        private class ServiceIndexEntryTelemetry : TelemetryEvent
        {
            public ServiceIndexEntryTelemetry(int NumSourceWithHttpResource, string eventName) : base(eventName)
            {
                this["NumHTTPSymbolPackageUpdateResourceWithHTTPSSource"] = NumSourceWithHttpResource;
            }
        }
    }
}
