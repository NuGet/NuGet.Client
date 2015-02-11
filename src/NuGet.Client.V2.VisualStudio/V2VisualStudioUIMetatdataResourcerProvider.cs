using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace NuGet.Client.V2.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(UIMetadataResource))]
    public class V2UIMetadataResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2UIMetadataResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new V2UIMetadataResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
