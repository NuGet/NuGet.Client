using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            : base(typeof(SearchLatestResource), "SearchLatestResourceV3Provider", "SearchLatestResourceV2Provider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SearchLatestResourceV3 curResource = null;
            ServiceIndexResourceV3 serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

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
