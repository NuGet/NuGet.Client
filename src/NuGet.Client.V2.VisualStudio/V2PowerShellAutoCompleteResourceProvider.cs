using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;

namespace NuGet.Client.V2.VisualStudio
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(PSAutoCompleteResource))]
    public class V2PowerShellAutoCompleteResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, PSAutoCompleteResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,PSAutoCompleteResource>();

        public override bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            PSAutoCompleteResource v2PowerShellAutoCompleteResource;
            if (!_cache.TryGetValue(source.PackageSource, out v2PowerShellAutoCompleteResource))
            {
                if (base.TryCreate(source, out resource))
                {

                    v2PowerShellAutoCompleteResource = new V2PowerShellAutoCompleteResource((V2Resource)resource);
                    _cache.TryAdd(source.PackageSource, v2PowerShellAutoCompleteResource);
                    resource = v2PowerShellAutoCompleteResource;
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
                resource = v2PowerShellAutoCompleteResource;
                return true;
                
            }
        }
    }
}
