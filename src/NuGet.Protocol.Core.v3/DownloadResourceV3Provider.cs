// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class DownloadResourceV3Provider : ResourceProvider
    {
        private readonly ConcurrentDictionary<PackageSource, DownloadResource> _cache;

        public DownloadResourceV3Provider()
            : base(typeof(DownloadResource), "DownloadResourceV3Provider", "DownloadResourceV2Provider")
        {
            _cache = new ConcurrentDictionary<PackageSource, DownloadResource>();
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                if (!_cache.TryGetValue(source.PackageSource, out curResource))
                {
                    var registrationResource = await source.GetResourceAsync<RegistrationResourceV3>(token);

                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    var client = new DataClient(messageHandlerResource.MessageHandler);

                    curResource = new DownloadResourceV3(client, registrationResource);

                    _cache.TryAdd(source.PackageSource, curResource);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
