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
    
    [NuGetResourceProviderMetadata(typeof(DepedencyInfoResource), "V2DependencyInfoResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2DependencyInfoResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, DepedencyInfoResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, DepedencyInfoResource>();

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DepedencyInfoResource v2DependencyInfoResource = null;
            if (!_cache.TryGetValue(source.PackageSource, out v2DependencyInfoResource))
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    v2DependencyInfoResource = new V2DependencyInfoResource(v2repo);
                    _cache.TryAdd(source.PackageSource, v2DependencyInfoResource);
                }
            }

            return Tuple.Create<bool, INuGetResource>(v2DependencyInfoResource != null, v2DependencyInfoResource);
        }
    }
}
