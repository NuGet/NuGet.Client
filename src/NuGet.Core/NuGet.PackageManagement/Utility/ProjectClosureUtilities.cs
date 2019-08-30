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
    public static class ProjectClosureUtilities
    {
        public static async Task<IEnumerable<PackageDependencyInfo>> GetDependencyInfoForPackageIdentitiesAsync(IEnumerable<PackageIdentity> packageIdentities,
            NuGetFramework nuGetFramework,
            DependencyInfoResource dependencyInfoResource,
            SourceCacheContext sourceCacheContext,
            ILogger logger,
            bool includeUnresolved,
            CancellationToken cancellationToken)
        {
            try
            {
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

        public static Func<IPackageWithDependants, bool> DirectDependenciesPredicate = dependencyItem => !dependencyItem.DependingPackages.Any();
        public static Func<IPackageWithDependants, bool> TransitiveDependenciesPredicate = dependencyItem => dependencyItem.DependingPackages.Any();

        public static void PopulateDependingPackages(IEnumerable<IPackageWithDependants> upgradeDependencyItems, IList<PackageDependencyInfo> packageDependencyInfos)
        {
            foreach (var packageDependencyInfo in packageDependencyInfos)
            {
                foreach (var dependency in packageDependencyInfo.Dependencies)
                {
                    var matchingDependencyItem = upgradeDependencyItems
                        .FirstOrDefault(d => (d.Identity.Id == dependency.Id) && (d.Identity.Version == dependency.VersionRange.MinVersion));
                    if (matchingDependencyItem != null)
                    {
                        matchingDependencyItem.DependingPackages.Add(new PackageIdentity(packageDependencyInfo.Id, packageDependencyInfo.Version));
                    }
                }
            }
        }
    }
}
