// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System.Diagnostics;

namespace NuGet.Protocol
{
    public class V3TotalsStatsResourceProvider : ResourceProvider
    {
        public V3TotalsStatsResourceProvider()
            : base(typeof(V3TotalsStatsResource), "V3TotalsStatsResourceProvider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken cancellationToken)
        {
            V3TotalsStatsResource totalsStatsResource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);

            if (serviceIndex != null)
            {
                // TODO: fix this for resource templates
                Debug.Assert(false, "Not Implemented");

                //ResourceSelector resourceSelector = new ResourceSelector(source);

                //IList<Uri> resourceUrls = serviceIndex[ServiceTypes.TotalStats];
                //Uri resourceUri = await resourceSelector.DetermineResourceUrlAsync(resourceUrls, cancellationToken);

                //var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(cancellationToken);
                //DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                //// construct a new resource
                //totalsStatsResource = new V3TotalsStatsResource(client, resourceUri);
            }

            return new Tuple<bool, INuGetResource>(totalsStatsResource != null, totalsStatsResource);
        }
    }
}
