using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    public abstract class UIMetadataResource : INuGetResource
    {
        /// <summary>
        /// Retrieve the full UI metadata for the given packages.
        /// </summary>
        /// <param name="packages"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="includeUnlisted"></param>
        /// <param name="token"></param>
        /// <returns></returns>
       public abstract Task<IEnumerable<UIPackageMetadata>> GetMetadata(IEnumerable<PackageIdentity> packages, bool includePrerelease, bool includeUnlisted, CancellationToken token);


       public async Task<IEnumerable<UIPackageMetadata>> GetMetadata(PackageIdentity package, bool includePrerelease, bool includeUnlisted, CancellationToken token)
       {
           return await GetMetadata(new PackageIdentity[] { package }, includePrerelease, includeUnlisted, token);
       }

        /// <summary>
        /// Returns all versions of a package
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
       public abstract Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token);
    }
}
