// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageDetailsUriResourceV3Provider : ResourceProvider
    {
        public PackageDetailsUriResourceV3Provider()
            : base(typeof(PackageDetailsUriResourceV3),
                  nameof(PackageDetailsUriResourceV3),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageDetailsUriResourceV3 resource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);
            if (serviceIndex != null)
            {
                var uri = serviceIndex.GetServiceEntryUri(ServiceTypes.PackageDetailsUriTemplate);

                if (uri != null &&
                    source.PackageSource.IsHttps &&
                    uri?.Scheme == Uri.UriSchemeHttp &&
                    uri?.Scheme != Uri.UriSchemeHttps)
                {
                    // Telemetry for HTTPS sources that have an HTTP resource
                    var telemetry = new ServiceIndexEntryTelemetry(1, "RestorePackageSourceSummary");
                    TelemetryActivity.EmitTelemetryEvent(telemetry);
                }

                resource = PackageDetailsUriResourceV3.CreateOrNull(uri?.OriginalString);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }

        private class ServiceIndexEntryTelemetry : TelemetryEvent
        {
            public ServiceIndexEntryTelemetry(int NumSourceWithHttpResource, string eventName) : base(eventName)
            {
                this["NumHTTPPackageDetailsUriResourceWithHTTPSSource"] = NumSourceWithHttpResource;
            }
        }
    }
}
