using NuGet.Client;
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
    /// <summary>
    /// Resource provider for V2 download.
    /// </summary>
    [NuGetResourceProviderMetadata(typeof(DownloadResource), "V2DownloadResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2DownloadResourceProvider : V2ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, DownloadResource> _cache = new ConcurrentDictionary<Configuration.PackageSource,DownloadResource>();

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource v2DownloadResource = null;

            if (!_cache.TryGetValue(source.PackageSource, out v2DownloadResource))
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    v2DownloadResource = new V2DownloadResource(v2repo);
                    _cache.TryAdd(source.PackageSource, v2DownloadResource);
                }
            }

            return new Tuple<bool, INuGetResource>(v2DownloadResource != null, v2DownloadResource);
        }
    }
}
