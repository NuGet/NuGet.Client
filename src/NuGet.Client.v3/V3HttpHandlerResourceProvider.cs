using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{

    
    [NuGetResourceProviderMetadata(typeof(HttpHandlerResource), "V3HttpHandlerResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V3HttpHandlerResourceProvider : INuGetResourceProvider
    {
        public V3HttpHandlerResourceProvider()
        {

        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            // Everyone gets a dataclient
            var curResource = new V3HttpHandlerResource(DataClient.DefaultHandler);

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
