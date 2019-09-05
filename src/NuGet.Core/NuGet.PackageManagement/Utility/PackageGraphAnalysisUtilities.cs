// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public static class PackageGraphAnalysisUtilities
    {
        public static Func<IPackageWithDependants, bool> TopLevelPackagesPredicate = dependencyItem => !dependencyItem.DependingPackages.Any();
        public static Func<IPackageWithDependants, bool> TransitivePackagesPredicate = dependencyItem => dependencyItem.DependingPackages.Any();

        /// <summary>
        /// Returns package dependency info for the given package identities in the given resource. It returns null if any protocol errors occur.
        /// For example, the feed is not accessible.
        /// </summary>
        /// <param name="packageIdentities">A collection of <see cref="PackageIdentity"/> to get info for.</param>
        /// <param name="nuGetFramework">framework for determining the dependency groups of packages</param>
        /// <param name="dependencyInfoResource">The resource to fetch dependency info from. Could be http/file feed/global packages folder/solution packages folder.</param>
        /// <param name="sourceCacheContext">Caching context. Only reallly applicable when the dependency info resource is http based</param>
        /// <param name="includeUnresolved">Whether to include unresolved packages in the list. If true, the unresolved packages will have an empty dependencies collection.</param>
        /// <param name="logger">logger</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>A collection of <see cref="PackageDependencyInfo"/>, null if a protocol exception happens.  </returns>
        public static async Task<IEnumerable<PackageDependencyInfo>> GetDependencyInfoForPackageIdentitiesAsync(IEnumerable<PackageIdentity> packageIdentities,
            NuGetFramework nuGetFramework,
            DependencyInfoResource dependencyInfoResource,
            SourceCacheContext sourceCacheContext,
            bool includeUnresolved,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);
                foreach (var package in packageIdentities)
                {
                    var packageDependencyInfo = await dependencyInfoResource.ResolvePackage(
                        package,
                        nuGetFramework,
                        sourceCacheContext,
                        logger,
                        cancellationToken);

                    if (packageDependencyInfo != null)
                    {
                        results.Add(packageDependencyInfo);
                    }
                    else if (includeUnresolved)
                    {
                        results.Add(new PackageDependencyInfo(package, null));
                    }
                }

                return results;
            }
            catch (NuGetProtocolException)
            {
                return null;
            }
        }

        /// <summary>
        /// Given a list of <see cref="PackageDependencyInfo"/> and a collection of <see cref="IPackageWithDependants"/> populates the latter collection with dependency info based on the <paramref name="packageDependencyInfos"/>.
        /// This method assumes that there is a 1-to-1 relationship between <paramref name="packageDependencyInfos"/> and <paramref name="packagesWithDependants"/>. 
        /// </summary>
        /// <remarks>This method will not handle null <paramref name="packageDependencyInfos"/> and <paramref name="packagesWithDependants"/></remarks>
        public static void PopulateDependants(IList<PackageDependencyInfo> packageDependencyInfos, IEnumerable<IPackageWithDependants> packagesWithDependants)
        {
            foreach (var packageDependencyInfo in packageDependencyInfos)
            {
                foreach (var dependency in packageDependencyInfo.Dependencies)
                {
                    var matchingDependencyItem = packagesWithDependants
                        .FirstOrDefault(d => (d.Identity.Id == dependency.Id) && (d.Identity.Version == dependency.VersionRange.MinVersion));
                    if (matchingDependencyItem != null)
                    {
                        matchingDependencyItem.DependingPackages.Add(packageDependencyInfo);
                    }
                }
            }
        }
        /// <summary>
        /// Given <paramref name="packageDependencyInfos"/> generates a collection of <see cref="IPackageWithDependants"/> with the dependants populated correctly.
        /// </summary>
        /// <returns>A collection of <see cref="IPackageWithDependants"/></returns>
        public static IEnumerable<IPackageWithDependants> GetPackagesWithDependants(IList<PackageDependencyInfo> packageDependencyInfos)
        {
            var packageWithDependants = packageDependencyInfos.Select(e => new PackageWithDependants(e));
            PopulateDependants(packageDependencyInfos, packageWithDependants);
            return packageWithDependants;
        }

        private class PackageWithDependants : IPackageWithDependants
        {
            public PackageIdentity Identity { get; }

            public IList<PackageIdentity> DependingPackages { get; }

            public PackageWithDependants(PackageIdentity packageIdentity) :
                this(packageIdentity, new List<PackageIdentity>())
            {
            }

            public PackageWithDependants(PackageIdentity identity, IList<PackageIdentity> dependingPackages)
            {
                Identity = identity ?? throw new ArgumentNullException(nameof(identity));
                DependingPackages = dependingPackages ?? throw new ArgumentNullException(nameof(dependingPackages));
            }
        }
    }
}
