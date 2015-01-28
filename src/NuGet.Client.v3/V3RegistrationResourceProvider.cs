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
    
    [NuGetResourceProviderMetadata(typeof(V3RegistrationResource), "V3RegistrationResource", NuGetResourceProviderPositions.Last)]
    public class V3RegistrationResourceProvider : INuGetResourceProvider
    {
        public V3RegistrationResourceProvider()
        {
   
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3RegistrationResource regResource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                Uri baseUrl = serviceIndex["RegistrationsBaseUrl"].FirstOrDefault();

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                regResource = new V3RegistrationResource(client, baseUrl);
            }

            return new Tuple<bool, INuGetResource>(regResource != null, regResource);
        }
    }
}
