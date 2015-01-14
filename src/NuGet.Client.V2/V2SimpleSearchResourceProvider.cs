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
    [NuGetResourceProviderMetadata(typeof(SimpleSearchResource))]
    public class V2SimpleSearchResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, SimpleSearchResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, SimpleSearchResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            SimpleSearchResource v2SimpleSearchResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2SimpleSearchResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2SimpleSearchResource = new V2SimpleSearchResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2SimpleSearchResource);
                    resource = v2SimpleSearchResource;
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
                resource = v2SimpleSearchResource;
                return true;
            }
        }
    }
}
