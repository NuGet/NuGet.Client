using NuGet.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    /// <summary>
    /// Resource provider for V2 download.
    /// </summary>
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(DownloadResource))]
    public class V2DownloadResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, DownloadResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,DownloadResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            DownloadResource v2DownloadResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2DownloadResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2DownloadResource = new V2DownloadResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2DownloadResource);
                    resource = v2DownloadResource;
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
                resource = v2DownloadResource;
                return true;    
            } 
        }
    }
}
