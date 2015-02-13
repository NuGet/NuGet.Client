using NuGet.Client.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2.VisualStudio
{
    [NuGetResourceProviderMetadata(typeof(SearchLatestResource), "V2SearchLatestResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2SearchLatestResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2SearchLatestResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new V2SearchLatestResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
