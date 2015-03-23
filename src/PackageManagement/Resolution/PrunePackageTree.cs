using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static IEnumerable<SourceDependencyInfo> PrunePreleaseForStableTargets(IEnumerable<SourceDependencyInfo> packages, IEnumerable<PackageIdentity> targets)
        {
            IEnumerable<SourceDependencyInfo> result = packages;

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

        public static IEnumerable<SourceDependencyInfo> PruneDisallowedVersions(IEnumerable<SourceDependencyInfo> packages, IEnumerable<PackageReference> packageReferences)
        {
            IEnumerable<SourceDependencyInfo> result = packages;
            foreach(var packageReference in packageReferences)
            {
                result = RemoveDisallowedVersions(result, packageReference);
            }

            return result;
        }

        /// <summary>
        /// Remove all versions of a package id from the list, except for the target version
        /// </summary>
        public static IEnumerable<SourceDependencyInfo> RemoveAllVersionsForIdExcept(IEnumerable<SourceDependencyInfo> packages, PackageIdentity target)
        {
            var comparer = VersionComparer.VersionRelease;

            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(target.Id, p.Id) ||
                (StringComparer.OrdinalIgnoreCase.Equals(target.Id, p.Id) && comparer.Equals(p.Version, target.Version)));
        }

        /// <summary>
        /// Keep only stable versions of a package
        /// </summary>
        public static IEnumerable<SourceDependencyInfo> RemoveAllPrereleaseVersionsForId(IEnumerable<SourceDependencyInfo> packages, string id)
        {
            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(id, p.Id) ||
                (StringComparer.OrdinalIgnoreCase.Equals(id, p.Id) && !p.Version.IsPrerelease));
        }

        /// <summary>
        /// Clear out all versions less than the minimuim. This can be used to prevent downgrading
        /// </summary>
        public static IEnumerable<SourceDependencyInfo> RemoveAllVersionsLessThan(IEnumerable<SourceDependencyInfo> packages, PackageIdentity minimum)
        {
            var comparer = VersionComparer.VersionRelease;

            return packages.Where(p => !StringComparer.OrdinalIgnoreCase.Equals(minimum.Id, p.Id) ||
                (StringComparer.OrdinalIgnoreCase.Equals(minimum.Id, p.Id) && comparer.Compare(p.Version, minimum.Version) >= 0));
        }

        // TODO: Consider removing elements from the collection and check if that is better in performance
        public static IEnumerable<SourceDependencyInfo> RemoveDisallowedVersions(IEnumerable<SourceDependencyInfo> packages, PackageReference packageReference)
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
