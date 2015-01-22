using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{

    
    [NuGetResourceProviderMetadata(typeof(HttpHandlerResource), "V3HttpHandlerResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V3HttpHandlerResourceProvider : INuGetResourceProvider
    {
        public V3HttpHandlerResourceProvider()
        {

        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            // Everyone gets a dataclient

            resource = new V3HttpHandlerResource(DataClient.DefaultHandler);

            return true;
        }
    }
}
