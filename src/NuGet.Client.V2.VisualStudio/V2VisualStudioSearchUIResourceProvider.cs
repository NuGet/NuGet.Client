using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;

namespace NuGet.Client.V2.VisualStudio
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(UISearchResource))]
    public class V2UISearchResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, UISearchResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,UISearchResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            UISearchResource v2UISearchResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2UISearchResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2UISearchResource = new V2UISearchResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2UISearchResource);
                    resource = v2UISearchResource;
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
                resource = v2UISearchResource;
                return true;                
            }
        }
    }
}