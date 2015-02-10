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
    
    [NuGetResourceProviderMetadata(typeof(MetadataResource), "V2MetadataResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2MetadataResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new V2MetadataResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
