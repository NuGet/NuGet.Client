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
    
    [NuGetResourceProviderMetadata(typeof(PSAutoCompleteResource), "V3PSAutoCompleteResourceProvider", "V2PSAutoCompleteResourceProvider")]
    public class V3PSAutoCompleteResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3PSAutoCompleteResourceProvider()
            : this(new DataClient())
        {

        }

        public V3PSAutoCompleteResourceProvider(DataClient client)
        {
            _client = client;
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3PSAutoCompleteResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                var regResource = await source.GetResourceAsync<V3RegistrationResource>(token);

                // construct a new resource
                curResource = new V3PSAutoCompleteResource(_client, serviceIndex, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
