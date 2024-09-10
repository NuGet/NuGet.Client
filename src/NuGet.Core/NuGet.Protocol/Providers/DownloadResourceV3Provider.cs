// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class DownloadResourceV3Provider : ResourceProvider
    {
        public DownloadResourceV3Provider()
            : base(typeof(DownloadResource), nameof(DownloadResourceV3Provider), "DownloadResourceV2FeedProvider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                var client = httpSourceResource.HttpSource;

                // Repository signature information init
                var repositorySignatureResource = await source.GetResourceAsync<RepositorySignatureResource>(token);
                repositorySignatureResource?.UpdateRepositorySignatureInfo();

                // If index.json contains a flat container resource use that to directly
                // construct package download urls.
                var uri = serviceIndex.GetServiceEntryUri(ServiceTypes.PackageBaseAddress);
                var packageBaseAddress = uri?.AbsoluteUri;

                // Telemetry for HTTPS sources that have an HTTP resource
                var telemetry = new ServiceIndexEntryTelemetry(
                    source.PackageSource.IsHttps &&
                    uri?.Scheme == Uri.UriSchemeHttp &&
                    uri?.Scheme != Uri.UriSchemeHttps ? 1 : 0,
                    "RestorePackageSourceSummary");
                TelemetryActivity.EmitTelemetryEvent(telemetry);

                if (packageBaseAddress != null)
                {
                    curResource = new DownloadResourceV3(source.PackageSource.Source, client, packageBaseAddress);
                }
                else
                {
                    // If there is no flat container resource fall back to using the registration resource to find
                    // the download url.
                    var registrationResource = await source.GetResourceAsync<RegistrationResourceV3>(token);
                    curResource = new DownloadResourceV3(source.PackageSource.Source, client, registrationResource);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }

        private class ServiceIndexEntryTelemetry : TelemetryEvent
        {
            public ServiceIndexEntryTelemetry(int NumSourceWithHttpResource, string eventName) : base(eventName)
            {
                this["NumHTTPDownloadResourceWithHTTPSSource"] = NumSourceWithHttpResource;
            }
        }
    }
}
