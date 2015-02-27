using NuGet.Protocol.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class V3MetadataResourceProvider : ResourceProvider
    {
        public V3MetadataResourceProvider()
            : base(typeof(MetadataResource), "V3MetadataResourceProvider", "V2MetadataResourceProvider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3MetadataResource curResource = null;
            V3RegistrationResource regResource = await source.GetResourceAsync<V3RegistrationResource>(token);

            if (regResource != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                curResource = new V3MetadataResource(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
