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
        private readonly ConcurrentDictionary<Configuration.PackageSource, MetadataResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,MetadataResource>();

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResource resource;
            if (!_cache.TryGetValue(source.PackageSource, out resource))
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    resource = new V2MetadataResource(v2repo);
                    _cache.TryAdd(source.PackageSource, resource);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
