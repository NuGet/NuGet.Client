using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Wrapper class consolidating common queries against a collection of packages
    /// </summary>
    internal class PackageCollection : IEnumerable<PackageCollectionItem>
    {
        private readonly PackageCollectionItem[] _packages;
        private readonly ISet<string> _uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PackageCollection(PackageCollectionItem[] packages)
        {
            _packages = packages;
            _uniqueIds.UnionWith(_packages.Select(p => p.Id));
        }

        public IEnumerator<PackageCollectionItem> GetEnumerator()
        {
            return ((IEnumerable<PackageCollectionItem>)_packages).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<PackageCollectionItem>)_packages).GetEnumerator();
        }

        public bool ContainsId(string packageId) => _uniqueIds.Contains(packageId);

        public static async Task<PackageCollection> FromProjectsAsync(IEnumerable<NuGetProject> projects, CancellationToken cancellationToken)
        {
            // Read package references from all projects.
            var tasks = projects
                .Select(project => project.GetInstalledPackagesAsync(cancellationToken));
            var packageReferences = await Task.WhenAll(tasks);

            // Group all package references for an id/version into a single item.
            var packages = packageReferences
                    .SelectMany(e => e)
                    .GroupBy(e => e.PackageIdentity, (key, group) => new PackageCollectionItem(key.Id, key.Version, group))
                    .ToArray();

            return new PackageCollection(packages);
        }
    }

    /// <summary>
    /// Common package queries implementes as extension methods
    /// </summary>
    internal static class PackageCollectionExtensions
    {
        public static NuGetVersion[] GetPackageVersions(this IEnumerable<PackageIdentity> packages, string packageId)
        {
            return packages
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, packageId))
                .Select(p => p.Version)
                .ToArray();
        }

        public static IEnumerable<IGrouping<string, NuGetVersion>> GroupById(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupBy(p => p.Id, p => p.Version, StringComparer.OrdinalIgnoreCase);
        }

        public static PackageIdentity[] GetLatest(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupById()
                .Select(g => new PackageIdentity(g.Key, g.MaxOrDefault()))
                .ToArray();
        }

        public static PackageIdentity[] GetEarliest(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupById()
                .Select(g => new PackageIdentity(g.Key, g.MinOrDefault()))
                .ToArray();
        }

        /// <summary>
        /// True if any package reference is AutoReferenced=true.
        /// </summary>
        public static bool IsAutoReferenced(this IEnumerable<PackageCollectionItem> packages, string id)
        {
            return packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id))
                .Any(e => e.IsAutoReferenced());
        }
    }

    internal static class PackageCollectionItemExtensions
    {
        public static bool IsAutoReferenced(this PackageCollectionItem package)
        {
            return package.PackageReferences
                .Select(e => e as BuildIntegratedPackageReference)
                .Any(e => e?.Dependency?.AutoReferenced == true);
        }
    }

    internal static class VersionCollectionExtensions
    {
        public static NuGetVersion MinOrDefault(this IEnumerable<NuGetVersion> versions)
        {
            return versions
                .OrderBy(v => v, VersionComparer.Default)
                .FirstOrDefault();
        }

        public static NuGetVersion MaxOrDefault(this IEnumerable<NuGetVersion> versions)
        {
            return versions
                .OrderByDescending(v => v, VersionComparer.Default)
                .FirstOrDefault();
        }
    }
}
