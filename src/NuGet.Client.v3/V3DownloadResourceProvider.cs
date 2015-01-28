using NuGet.Configuration;
using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    
    [NuGetResourceProviderMetadata(typeof(DownloadResource), "V3DownloadResourceProvider", "V2DownloadResourceProvider")]
    public class V3DownloadResourceProvider : INuGetResourceProvider
    {
        private readonly ConcurrentDictionary<PackageSource, DownloadResource> _cache;

        public V3DownloadResourceProvider()
        {
            _cache = new ConcurrentDictionary<PackageSource, DownloadResource>();
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                if (!_cache.TryGetValue(source.PackageSource, out curResource))
                {
                    var registrationResource = await source.GetResourceAsync<V3RegistrationResource>(token);

                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                    curResource = new V3DownloadResource(client, registrationResource);

                    _cache.TryAdd(source.PackageSource, curResource);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
