// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.ApiApps
{
    public class ApiAppSearchResourceProvider : ResourceProvider
    {
        public ApiAppSearchResourceProvider()
            : base(typeof(ApiAppSearchResource), nameof(ApiAppSearchResource), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ApiAppSearchResource resource = null;

            var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>();

            if (messageHandlerResource != null
                && serviceIndex != null)
            {
                var endpoints = serviceIndex["ApiAppSearchQueryService"];

                if (endpoints.Any())
                {
                    var rawSearch = new RawSearchResourceV3(messageHandlerResource.MessageHandler, endpoints);

                    resource = new ApiAppSearchResource(rawSearch);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
