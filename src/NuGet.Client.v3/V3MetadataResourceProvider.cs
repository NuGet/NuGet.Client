using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    
    [NuGetResourceProviderMetadata(typeof(MetadataResource), "V3MetadataResourceProvider", "V2MetadataResourceProvider")]
    public class V3MetadataResourceProvider : INuGetResourceProvider
    {
        public V3MetadataResourceProvider()
        {

        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
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
