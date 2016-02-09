using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using System.Globalization;

namespace NuGet.Protocol.Core.v3
{
    public class PackageUpdateResourceV3Provider : ResourceProvider
    {
        public PackageUpdateResourceV3Provider()
            : base(
                  typeof(PackageUpdateResource),
                  nameof(PackageUpdateResourceV3Provider),
                  "PushCommandResourceV2Provider")
        { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            PackageUpdateResource packageUpdateResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var baseUrl = serviceIndex[ServiceTypes.PackagePublish].FirstOrDefault();

                HttpSource httpSource = null;
                var sourceUri = baseUrl?.AbsoluteUri;
                if (!string.IsNullOrEmpty(sourceUri))
                {
                    if (!(new Uri(sourceUri)).IsFile)
                    {
                        var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                        httpSource = httpSourceResource.HttpSource;
                    }
                    packageUpdateResource = new PackageUpdateResource(sourceUri, httpSource);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.PackageServerEndpoint_NotSupported,
                        source));
                }
            }

            var result = new Tuple<bool, INuGetResource>(packageUpdateResource != null, packageUpdateResource);
            return result;
        }
    }
}
