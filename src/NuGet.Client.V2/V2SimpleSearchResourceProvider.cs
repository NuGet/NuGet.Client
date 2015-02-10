using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    
    [NuGetResourceProviderMetadata(typeof(SimpleSearchResource), "V2SimpleSearchResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2SimpleSearchResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2SimpleSearchResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new V2SimpleSearchResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
