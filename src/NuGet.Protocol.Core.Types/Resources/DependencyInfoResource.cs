using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Provides methods for resolving a package and its dependencies. This might change based on the new dependency resolver.
    /// </summary>
    public abstract class DepedencyInfoResource : INuGetResource
    {
        /// <summary>
        /// Check if the given package identity is present in the current repository. This would used to check if correct package Id/Version is passed before resolving dependencies.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease)
        {
            return await ResolvePackages(packages, projectFramework, includePrerelease, CancellationToken.None);
        }

        /// <summary>
        /// Check if the given package identity is present in the current repository. This would used to check if correct package Id/Version is passed before resolving dependencies.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public abstract Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token);


        /// <summary>
        /// Find all packages with the given name and their dependencies
        /// </summary>
        public async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            return await ResolvePackages(new string[] { packageId }, projectFramework, includePrerelease, token);
        }

        /// <summary>
        /// Find all packages with the given name and their dependencies
        /// </summary>
        public abstract Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<string> packageIds, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token);
    }
}
