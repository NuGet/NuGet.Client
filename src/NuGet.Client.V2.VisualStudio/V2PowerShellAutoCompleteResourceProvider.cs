using System.ComponentModel.Composition;
using NuGet.Client.VisualStudio;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace NuGet.Client.V2.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(PSAutoCompleteResource), "V2PowerShellAutoCompleteResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2PowerShellAutoCompleteResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, V2PowerShellAutoCompleteResource> _cache = new ConcurrentDictionary<Configuration.PackageSource, V2PowerShellAutoCompleteResource>();

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2PowerShellAutoCompleteResource resource = null;
            if (!_cache.TryGetValue(source.PackageSource, out resource))
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    resource = new V2PowerShellAutoCompleteResource(v2repo);
                    _cache.TryAdd(source.PackageSource, resource);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
