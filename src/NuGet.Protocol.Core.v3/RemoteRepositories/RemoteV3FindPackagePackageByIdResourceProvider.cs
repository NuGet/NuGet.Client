using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV3FindPackagePackageByIdResourceProvider : ResourceProvider
    {
        public RemoteV3FindPackagePackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                   nameof(RemoteV3FindPackagePackageByIdResourceProvider), 
                   before: nameof(RemoteV2FindPackageByIdResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository sourceRepository, CancellationToken token)
        {
            INuGetResource resource = null;

            var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();

            if (serviceIndexResource != null)
            {
                resource = new RemoteV3FindPackageByIdResource(sourceRepository);
            }

            return Tuple.Create(resource != null, resource);
        }
    }
}