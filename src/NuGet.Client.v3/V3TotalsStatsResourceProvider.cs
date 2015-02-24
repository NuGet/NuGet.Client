using NuGet.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3TotalsStatsResource), "V3TotalsStatsResourceProvider")]
    public class V3TotalsStatsResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken cancellationToken)
        {
            V3TotalsStatsResource totalsStatsResource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(cancellationToken);

            if (serviceIndex != null)
            {
                IList<Uri> resourceUrls = serviceIndex[ServiceTypes.TotalStats];
                Uri resourceUri = await ResourceSelector.DetermineResourceUrlAsync(resourceUrls, cancellationToken);

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(cancellationToken);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                totalsStatsResource = new V3TotalsStatsResource(client, resourceUri);
            }

            return new Tuple<bool, INuGetResource>(totalsStatsResource != null, totalsStatsResource);
        }
    }
}
