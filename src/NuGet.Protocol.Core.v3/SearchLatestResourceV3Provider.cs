// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class SearchLatestResourceV3Provider : ResourceProvider
    {
        private readonly DataClient _client;

        public SearchLatestResourceV3Provider()
            : this(new DataClient())
        {
        }

        public SearchLatestResourceV3Provider(DataClient client)
            : base(typeof(SearchLatestResource),
                  nameof(SearchLatestResourceV3Provider),
                  "SearchLatestResourceV2Provider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SearchLatestResourceV3 curResource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var rawSearch = await source.GetResourceAsync<RawSearchResourceV3>(token);

                if (rawSearch != null)
                {
                    curResource = new SearchLatestResourceV3(rawSearch);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
