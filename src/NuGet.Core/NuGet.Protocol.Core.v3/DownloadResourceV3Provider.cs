// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class DownloadResourceV3Provider : ResourceProvider
    {
        public DownloadResourceV3Provider()
            : base(typeof(DownloadResource), nameof(DownloadResourceV3Provider), "DownloadResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                var client = new DataClient(messageHandlerResource);

                // If index.json contains a flat container resource use that to directly 
                // construct package download urls.
                var packageBaseAddress = serviceIndex[ServiceTypes.PackageBaseAddress].FirstOrDefault()?.AbsoluteUri;

                if (packageBaseAddress != null)
                {
                    curResource = new DownloadResourceV3(client, packageBaseAddress);
                }
                else
                {
                    // If there is no flat container resource fall back to using the registration resource to find
                    // the download url.
                    var registrationResource = await source.GetResourceAsync<RegistrationResourceV3>(token);
                    curResource = new DownloadResourceV3(client, registrationResource);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
