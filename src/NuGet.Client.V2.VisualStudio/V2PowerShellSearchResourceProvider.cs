using NuGet.Client.VisualStudio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2.VisualStudio
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(PSSearchResource))]
    public class V2PSSearchResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, PSSearchResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, PSSearchResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            PSSearchResource v2PSSearchResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2PSSearchResource))
            {
                UISearchResource uiSearchResource = source.GetResource<UISearchResource>();
                if (uiSearchResource != null)
                {
                    v2PSSearchResource = new V2PowerShellSearchResource(uiSearchResource);
                    _cache.TryAdd(source.PackageSource, v2PSSearchResource);
                    resource = v2PSSearchResource;
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
                resource = v2PSSearchResource;
                return true;
            }
        }
    }
}
