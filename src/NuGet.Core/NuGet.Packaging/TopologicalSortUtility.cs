// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class TopologicalSortUtility
    {
        private static readonly PackageInfoComparer DefaultComparer = new PackageInfoComparer();
        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        public static IReadOnlyList<PackageDependencyInfo> SortPackagesByDependencyOrder(
            IEnumerable<PackageDependencyInfo> packages)
        {
            var lookup = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
            //Deduplicate references
            foreach (var package in packages)
            {
                var id = package.Id;
                if (!lookup.ContainsKey(id))
                {
                    lookup.Add(id, new PackageInfo(package));
                }
            }

            // Extract the deduplicated values
            var toSort = lookup.Values.ToArray();
            var sorted = new List<PackageDependencyInfo>(toSort.Length);

            CalculateRelationships(toSort, lookup);

            for (var i = 0; i < toSort.Length; i++)
            {
                Array.Sort(toSort, i, toSort.Length - i, DefaultComparer);
                // take the child with the lowest number of children
                var package = toSort[i];
                sorted.Add(package.Package);
                UpdateChildCounts(package);
            }

            // the list is ordered by parents first, reverse to run children first
            sorted.Reverse();

            return sorted;
        }

        private static void UpdateChildCounts(PackageInfo package)
        {
            // Decrement the parent count for each child of this package.
            var children = package.Children;
            if (children != null)
            {
                var count = children.Count;
                for (var i = 0; i < count; i++)
                {
                    children[i].ActiveParents--;
                }
            }
        }

        private static void CalculateRelationships(PackageInfo[] packages, Dictionary<string, PackageInfo> lookup)
        {
            foreach (var package in packages)
            {
                var deps = package.Package.Dependencies;
                var dependencies = deps as PackageDependency[] ?? deps.ToArray();

                foreach (var dependency in dependencies)
                {
                    var id = dependency.Id;
                    if (lookup.TryGetValue(id, out var dependencyPackage))
                    {
                        // Mark the current package as a parent
                        var parents = dependencyPackage.Parents;
                        if (parents == null)
                        {
                            parents = new List<PackageInfo>();
                            dependencyPackage.Parents = parents;
                        }
                        parents.Add(package);

                        // Add a child package for the current package
                        var packageChildren = package.Children;
                        if (packageChildren == null)
                        {
                            packageChildren = new List<PackageInfo>();
                            package.Children = packageChildren;
                        }
                        packageChildren.Add(dependencyPackage);
                    }
                }
            }

            foreach (var package in packages)
            {
                package.ActiveParents = package.Parents?.Count ?? 0;
            }
        }

        private class PackageInfoComparer : IComparer<PackageInfo>
        {
            public int Compare(PackageInfo x, PackageInfo y)
            {
                // Order packages by parent count
                if (x.ActiveParents < y.ActiveParents)
                {
                    return -1;
                }
                if (x.ActiveParents > y.ActiveParents)
                {
                    return 1;
                }
                return StringComparer.OrdinalIgnoreCase.Compare(x.Package.Id, y.Package.Id);
            }
        }

        [DebuggerDisplay("{Package.Id} Active: {ActiveParents}")]
        private sealed class PackageInfo
        {
            public PackageInfo(PackageDependencyInfo package)
            {
                Package = package;
                ActiveParents = 0;
            }

            public PackageDependencyInfo Package;
            public int ActiveParents;
            public List<PackageInfo> Parents;
            public List<PackageInfo> Children;
        }
    }
}
