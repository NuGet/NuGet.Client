using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(DepedencyInfoResource))]
    public class V2DependencyInfoResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, DepedencyInfoResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, DepedencyInfoResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            DepedencyInfoResource v2DependencyInfoResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2DependencyInfoResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2DependencyInfoResource = new V2DependencyInfoResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2DependencyInfoResource);
                    resource = v2DependencyInfoResource;
                    return true;
                }
                else
                {
                    resource = null;
                    return false;
                }
            }
            else
            {
                resource = v2DependencyInfoResource;
                return true;
            }
        }
    }
}
