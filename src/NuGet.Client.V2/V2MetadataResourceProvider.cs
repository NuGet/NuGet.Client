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
    [NuGetResourceProviderMetadata(typeof(MetadataResource))]
    public class V2MetadataResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, MetadataResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,MetadataResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            MetadataResource v2MetadataResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2MetadataResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2MetadataResource = new V2MetadataResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2MetadataResource);
                    resource = v2MetadataResource;
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
                resource = v2MetadataResource;
                return true;
            } 
        }
    }
}
