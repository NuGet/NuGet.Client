using NuGet.Configuration;
using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(DownloadResource), "V3DownloadResourceProvider", "V2DownloadResourceProvider")]
    public class V3DownloadResourceProvider : INuGetResourceProvider
    {
        private readonly ConcurrentDictionary<PackageSource, DownloadResource> _cache;

        public V3DownloadResourceProvider()
        {
            _cache = new ConcurrentDictionary<PackageSource, DownloadResource>();
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            DownloadResource downloadResource = null;

            var serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                if (!_cache.TryGetValue(source.PackageSource, out downloadResource))
                {
                    var registrationResource = source.GetResource<V3RegistrationResource>();

                    DataClient client = new DataClient(source.GetResource<HttpHandlerResource>().MessageHandler);

                    downloadResource = new V3DownloadResource(client, registrationResource);

                    _cache.TryAdd(source.PackageSource, downloadResource);
                }
            }

            resource = downloadResource;
            return downloadResource != null;
        }
    }
}
