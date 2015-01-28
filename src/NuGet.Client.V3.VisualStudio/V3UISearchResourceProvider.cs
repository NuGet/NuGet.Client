using NuGet.Client.V3;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(UISearchResource), "V3UISearchResourceProvider", "V2UISearchResourceProvider")]
    public class V3UISearchResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3UISearchResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UISearchResourceProvider(DataClient client)
        {
            _client = client;
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
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
