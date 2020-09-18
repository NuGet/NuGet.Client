// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Wrapper class consolidating common queries against a collection of packages
    /// </summary>
    public sealed class PackageCollection : IEnumerable<PackageCollectionItem>
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

        public static async Task<PackageCollection> FromProjectsAsync(IEnumerable<IProjectContextInfo> projects, CancellationToken cancellationToken)
        {
            // Read package references from all projects.
            IEnumerable<Task<IReadOnlyCollection<IPackageReferenceContextInfo>>>? tasks = projects
                .Select(project => project.GetInstalledPackagesAsync(cancellationToken).AsTask());
            IEnumerable<IPackageReferenceContextInfo>[]? packageReferences = await Task.WhenAll(tasks);

            // Group all package references for an id/version into a single item.
            PackageCollectionItem[]? packages = packageReferences
                .SelectMany(e => e)
                .GroupBy(e => e.Identity, (key, group) => new PackageCollectionItem(key.Id, key.Version, group))
                .ToArray();

            return new PackageCollection(packages);
        }

        public static async Task<PackageCollection> FromProjectsTransitiveAsync(IEnumerable<IProjectContextInfo> projects, CancellationToken cancellationToken)
        {
            // Read transitive package references from all package reference style projects.
            IEnumerable<Task<IReadOnlyCollection<IPackageReferenceContextInfo>>>? tasks = projects
                .Select(project => project.GetTransitivePackagesAsync(cancellationToken).AsTask());
            IEnumerable<IPackageReferenceContextInfo>[]? transitivePackageReferences = await Task.WhenAll(tasks);

            // Group all package references for an id/version into a single item.
            PackageCollectionItem[]? packages = transitivePackageReferences
                .SelectMany(e => e)
                .GroupBy(e => e.Identity, (key, group) => new PackageCollectionItem(key.Id, key.Version, group))
                .ToArray();

            return new PackageCollection(packages);
        }
    }
}
