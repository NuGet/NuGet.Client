using NuGet.Protocol.Data;
using NuGet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class V3UISearchResourceProvider : ResourceProvider
    {
        private readonly DataClient _client;

        public V3UISearchResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UISearchResourceProvider(DataClient client)
            : base(typeof(UISearchResource), "V3UISearchResourceProvider", "V2UISearchResourceProvider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3UISearchResource curResource = null;
            V3ServiceIndexResource serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                var rawSearch = await source.GetResourceAsync<V3RawSearchResource>(token);
                var metadataResource = await source.GetResourceAsync<UIMetadataResource>(token);

                curResource = new V3UISearchResource(rawSearch, metadataResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
