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
    
    [NuGetResourceProviderMetadata(typeof(UIMetadataResource), "V3UIMetadataResourceProvider", "V2UIMetadataResourceProvider")]
    public class V3UIMetadataResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3UIMetadataResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UIMetadataResourceProvider(DataClient client)
        {
            _client = client;
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3UIMetadataResource curResource = null;

            if (await source.GetResourceAsync<V3ServiceIndexResource>(token) != null)
            {
                var regResource = await source.GetResourceAsync<V3RegistrationResource>();
                var reportAbuseResource = await source.GetResourceAsync<V3ReportAbuseResource>();

                // construct a new resource
                curResource = new V3UIMetadataResource(_client, regResource, reportAbuseResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
