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
    
    [NuGetResourceProviderMetadata(typeof(SearchLatestResource), "V3SearchLatestResourceProvider", "V2SearchLatestResourceProvider")]
    public class V3SearchLatestResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3SearchLatestResourceProvider()
            : this(new DataClient())
        {

        }

        public V3SearchLatestResourceProvider(DataClient client)
        {
            _client = client;
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3SearchLatestResource curResource = null;
            V3ServiceIndexResource serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                var rawSearch = await source.GetResourceAsync<V3RawSearchResource>(token);
                V3UIMetadataResource metadataResource = (await source.GetResourceAsync<UIMetadataResource>(token)) as V3UIMetadataResource;

                if (rawSearch != null && metadataResource != null)
                {
                    curResource = new V3SearchLatestResource(rawSearch, metadataResource);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
