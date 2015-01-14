using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;

namespace NuGet.Client.V2.VisualStudio
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(UIMetadataResource))]
    public class V2UIMetadataResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, UIMetadataResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, UIMetadataResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            UIMetadataResource v2UIMetadataResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2UIMetadataResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2UIMetadataResource = new V2UIMetadataResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2UIMetadataResource);
                    resource = v2UIMetadataResource;
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
                resource = v2UIMetadataResource;
                return true;
                
            }
        }
    }
}
