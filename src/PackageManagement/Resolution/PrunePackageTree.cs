// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helpers to reduce down the gathered package dependency info to the allowed set
    /// </summary>
    public static class PrunePackageTree
    {
        /// <summary>
        /// Remove all prerelease packages for stable targets
        /// </summary>
        public static IEnumerable<SourcePackageDependencyInfo> PrunePreleaseForStableTargets(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<PackageIdentity> targets, IEnumerable<PackageIdentity> packagesToInstall)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectAllowedFromTargets(targets, allowed);
            CollectAllowedFromDependenciesOfPackagesToInstall(packages, packagesToInstall, allowed);

            return packages.Where(p => !(p.HasVersion && p.Version.IsPrerelease) || allowed.Contains(p.Id));
        }

        private static void CollectAllowedFromTargets(IEnumerable<PackageIdentity> targets, HashSet<string> allowed)
        {
            foreach (var p in targets.Where(p => p.HasVersion && p.Version.IsPrerelease))
            {
                allowed.Add(p.Id);
            }
        }

        private static void CollectAllowedFromDependenciesOfPackagesToInstall(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<PackageIdentity> packagesToInstall, HashSet<string> allowed)
        {
            var prereleasePackageToInstall = new HashSet<PackageIdentity>(packagesToInstall.Where(p => p.HasVersion && p.Version.IsPrerelease), PackageIdentityComparer.Default);

            var maxDepth = packages.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            foreach (var packageToInstall in packages.Where(p => prereleasePackageToInstall.Contains(p)))
            {
                int depth = 0;
                WalkDependencies(packages, packageToInstall, allowed, depth, maxDepth);
            }
        }

        private static void WalkDependencies(IEnumerable<SourcePackageDependencyInfo> packages, SourcePackageDependencyInfo packageToInstall, HashSet<string> allowed, int depth, int maxDepth)
        {
            if (depth > maxDepth)
            {
                return;
            }

            if (packageToInstall.Dependencies != null)
            {
                foreach (var dependency in packageToInstall.Dependencies)
                {
                    allowed.Add(dependency.Id);
                    foreach (SourcePackageDependencyInfo dependentPackage in packages.Where(p => p.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        WalkDependencies(packages, dependentPackage, allowed, depth + 1, maxDepth);
                    }
                }
            }
        }

        public static IEnumerable<SourcePackageDependencyInfo> PruneDowngrades(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<Packaging.PackageReference> packageReferences)
        {
            // prune every package that is less that the currently installed package

            IDictionary<string, NuGetVersion> installed = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var packageReference in packageReferences)
            {
                installed.Add(packageReference.PackageIdentity.Id, packageReference.PackageIdentity.Version);
            }

            return packages.Where(package => 
                (package.HasVersion && installed.ContainsKey(package.Id)) 
                    ? 
                installed[package.Id] <= package.Version : true);
        }

        public static IEnumerable<SourcePackageDependencyInfo> PruneDisallowedVersions(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<Packaging.PackageReference> packageReferences)
        {
            var result = packages;
            foreach (var packageReference in packageReferences)
            {
                result = RemoveDisallowedVersions(result, packageReference);
            }

            return result;
        }

        /// <summary>
        /// Remove all versions of a package id from the list, except for the target version
        /// </summary>
        public static IEnumerable<SourcePackageDependencyInfo> RemoveAllVersionsForIdExcept(IEnumerable<SourcePackageDependencyInfo> packages, PackageIdentity target)
        {
            var comparer = VersionComparer.VersionRelease;

            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(target.Id, p.Id) ||
                                       (StringComparer.OrdinalIgnoreCase.Equals(target.Id, p.Id) && comparer.Equals(p.Version, target.Version)));
        }

        /// <summary>
        /// Keep only stable versions of a package
        /// </summary>
        public static IEnumerable<SourcePackageDependencyInfo> RemoveAllPrereleaseVersionsForId(IEnumerable<SourcePackageDependencyInfo> packages, string id)
        {
            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(id, p.Id) ||
                                       (StringComparer.OrdinalIgnoreCase.Equals(id, p.Id) && !p.Version.IsPrerelease));
        }

        /// <summary>
        /// Clear out all versions less than the minimuim. This can be used to prevent downgrading
        /// </summary>
        public static IEnumerable<SourcePackageDependencyInfo> RemoveAllVersionsLessThan(IEnumerable<SourcePackageDependencyInfo> packages, PackageIdentity minimum)
        {
            var comparer = VersionComparer.VersionRelease;

            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(minimum.Id, p.Id) ||
                                       (StringComparer.OrdinalIgnoreCase.Equals(minimum.Id, p.Id) && comparer.Compare(p.Version, minimum.Version) >= 0));
        }

        // TODO: Consider removing elements from the collection and check if that is better in performance
        public static IEnumerable<SourcePackageDependencyInfo> RemoveDisallowedVersions(IEnumerable<SourcePackageDependencyInfo> packages, Packaging.PackageReference packageReference)
        {
            if (packageReference.AllowedVersions != null)
            {
                return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p.Id, packageReference.PackageIdentity.Id)
                                           || packageReference.AllowedVersions.Satisfies(p.Version));
            }
            return packages;
        }
    }
}
