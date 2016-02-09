using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public class PushCommandResourceV2Provider : ResourceProvider
    {
        public PushCommandResourceV2Provider()
            : base(
                  typeof(PushCommandResource),
                  nameof(PushCommandResourceV2Provider),
                  NuGetResourceProviderPositions.Last)
        { }

        public async override Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            HttpSource httpSource = null;
            string sourceUri = source.PackageSource?.Source;
            if (source.PackageSource.IsHttp)
            {
                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                httpSource = httpSourceResource.HttpSource;
            }

            var pushCommandResource = new PushCommandResource(sourceUri, httpSource);
                
            var result = new Tuple<bool, INuGetResource>(pushCommandResource != null, pushCommandResource);
            return result;
        }
    }
}
