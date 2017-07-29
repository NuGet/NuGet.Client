// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            CalcuateRelationships(toSort, lookup);

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
            var children = package.Children;
            if (children != null)
            {
                var count = children.Count;
                for (var i = 0; i < count; i++)
                {
                    children[i].ActiveChildren--;
                }
            }
        }

        private static void CalcuateRelationships(PackageInfo[] packages, Dictionary<string, PackageInfo> lookup)
        {
            foreach (var package in packages)
            {
                var deps = package.Package.Dependencies;
                var dependencies = deps as PackageDependency[] ?? deps.ToArray();

                foreach (var dependency in dependencies)
                {
                    var id = dependency.Id;
                    if (lookup.TryGetValue(id, out var parent))
                    {
                        var children = parent.Children;
                        if (children == null)
                        {
                            children = new List<PackageInfo>();
                            parent.Children = children;
                        }
                        children.Add(package);
                    }
                }
            }

            foreach (var package in packages)
            {
                package.ActiveChildren = package.Children?.Count ?? 0;
            }
        }

        private class PackageInfoComparer : IComparer<PackageInfo>
        {
            public int Compare(PackageInfo x, PackageInfo y)
            {
                // Order packages by parent count
                if (x.ActiveChildren < y.ActiveChildren)
                {
                    return -1;
                }
                if (x.ActiveChildren > y.ActiveChildren)
                {
                    return 1;
                }
                return StringComparer.OrdinalIgnoreCase.Compare(x.Package.Id, y.Package.Id);
            }
        }

        private sealed class PackageInfo
        {
            public PackageInfo(PackageDependencyInfo package)
            {
                Package = package;
                ActiveChildren = 0;
            }

            public PackageDependencyInfo Package;
            public int ActiveChildren;
            public List<PackageInfo> Children;
        }
    }
}
