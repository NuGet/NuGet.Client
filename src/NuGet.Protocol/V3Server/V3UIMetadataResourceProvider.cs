using NuGet.Protocol.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class V3UIMetadataResourceProvider : ResourceProvider
    {
        private readonly DataClient _client;

        public V3UIMetadataResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UIMetadataResourceProvider(DataClient client)
            : base(typeof(UIMetadataResource), "V3UIMetadataResourceProvider", "V2UIMetadataResourceProvider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
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
