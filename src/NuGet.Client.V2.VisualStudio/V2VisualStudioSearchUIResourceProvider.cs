using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace NuGet.Client.V2.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(UISearchResource), "V2UISearchResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2UISearchResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2UISearchResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new V2UISearchResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}