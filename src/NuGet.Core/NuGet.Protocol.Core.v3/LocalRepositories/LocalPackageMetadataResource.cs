using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalPackageMetadataResource : PackageMetadataResource
    {
        private readonly FindLocalPackagesResource _localResource;

        public LocalPackageMetadataResource(FindLocalPackagesResource localResource)
        {
            if (localResource == null)
            {
                throw new ArgumentNullException(nameof(localResource));
            }

            _localResource = localResource;
        }

        public override Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            ILogger log,
            CancellationToken token)
        {
            // All packages are considered listed within a local repo

            return Task.Run<IEnumerable<IPackageSearchMetadata>>(() =>
            {
                return _localResource.FindPackagesById(packageId, log, token)
                    .Where(p => includePrerelease || !p.Identity.Version.IsPrerelease)
                    .Select(GetPackageMetadata)
                    .ToList();
            },
            token);
        }

        private static IPackageSearchMetadata GetPackageMetadata(LocalPackageInfo package) => new LocalPackageSearchMetadata(package);
    }
}
