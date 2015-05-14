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
        public static IEnumerable<SourcePackageDependencyInfo> PrunePreleaseForStableTargets(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<PackageIdentity> targets)
        {
            var result = packages;

            foreach (var group in targets.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
            {
                // remove prerelease versions for targets that are non-prerelease themselves
                if (!targets.Any(p => p.HasVersion && p.Version.IsPrerelease))
                {
                    result = RemoveAllPrereleaseVersionsForId(result, group.Key);
                }
            }

            return result;
        }

        public static IEnumerable<SourcePackageDependencyInfo> PruneDisallowedVersions(IEnumerable<SourcePackageDependencyInfo> packages, IEnumerable<PackageReference> packageReferences)
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
        public static IEnumerable<SourcePackageDependencyInfo> RemoveDisallowedVersions(IEnumerable<SourcePackageDependencyInfo> packages, PackageReference packageReference)
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
