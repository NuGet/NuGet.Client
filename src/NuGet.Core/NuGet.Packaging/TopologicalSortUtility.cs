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
        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <param name="comparer">Comparer for Ids.</param>
        /// <param name="getId">Retrieve the id of the item.</param>
        /// <param name="getDependencies">Retrieve dependency ids.</param>
        /// <returns></returns>
        public static IReadOnlyList<T> SortPackagesByDependencyOrder<T>(
            IEnumerable<T> items,
            StringComparer comparer,
            Func<T, string> getId,
            Func<T, string[]> getDependencies) where T : class
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            if (getId == null)
            {
                throw new ArgumentNullException(nameof(getId));
            }

            if (getDependencies == null)
            {
                throw new ArgumentNullException(nameof(getDependencies));
            }

            var lookup = new Dictionary<string, ItemDependencyInfo<T>>(comparer);
            var itemInfos = new List<ItemDependencyInfo<T>>();

            foreach (var item in items)
            {
                var id = getId(item);
                var deps = getDependencies(item);

                if (!lookup.ContainsKey(id))
                {
                    var itemInfo = new ItemDependencyInfo<T>(id, deps, item);
                    lookup.Add(id, itemInfo);
                    itemInfos.Add(itemInfo);
                }
            }

            return SortPackagesByDependencyOrder(itemInfos, lookup, comparer);
        }

        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        public static IReadOnlyList<PackageDependencyInfo> SortPackagesByDependencyOrder(
            IEnumerable<PackageDependencyInfo> packages)
        {
            return SortPackagesByDependencyOrder(
                packages,
                StringComparer.OrdinalIgnoreCase,
                GetPackageDependencyInfoId,
                GetPackageDependencyInfoDependencies);
        }

        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        private static IReadOnlyList<T> SortPackagesByDependencyOrder<T>(
            List<ItemDependencyInfo<T>> items,
            Dictionary<string, ItemDependencyInfo<T>> lookup,
            StringComparer comparer) where T : class
        {
            var itemComparer = new PackageInfoComparer<T>(comparer);
            var sorted = new List<T>(items.Count);

            CalculateRelationships(items, lookup);

            for (var i = 0; i < items.Count; i++)
            {
                var selectedIndex = i;
                for (var candidateIndex = i + 1; candidateIndex < items.Count; candidateIndex++)
                {
                    if (itemComparer.Compare(items[candidateIndex], items[selectedIndex]) < 0)
                    {
                        selectedIndex = candidateIndex;
                    }
                }

                var package = items[selectedIndex];
                items[selectedIndex] = items[i];
                items[i] = package;

                sorted.Add(package.Item);
                UpdateChildCounts(package);
            }

            // The packages are selected parents first, so reverse to run children first.
            sorted.Reverse();

            return sorted;
        }

        private static void UpdateChildCounts<T>(ItemDependencyInfo<T> package) where T : class
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

        private static void CalculateRelationships<T>(
            List<ItemDependencyInfo<T>> packages,
            Dictionary<string, ItemDependencyInfo<T>> lookup) where T : class
        {
            foreach (var package in packages)
            {
                var dependencies = package.DependencyIds ?? Array.Empty<string>();

                for (var i = 0; i < dependencies.Length; i++)
                {
                    var id = dependencies[i];
                    if (lookup.TryGetValue(id, out var dependencyPackage))
                    {
                        dependencyPackage.ActiveParents++;

                        // Add a child package for the current package
                        var packageChildren = package.Children;
                        if (packageChildren == null)
                        {
                            packageChildren = new List<ItemDependencyInfo<T>>(dependencies.Length - i);
                            package.Children = packageChildren;
                        }
                        packageChildren.Add(dependencyPackage);
                    }
                }
            }
        }


        private static string GetPackageDependencyInfoId(PackageDependencyInfo info)
        {
            return info.Id;
        }

        private static string[] GetPackageDependencyInfoDependencies(PackageDependencyInfo info)
        {
            return info.Dependencies.Select(e => e.Id).ToArray();
        }

        private class PackageInfoComparer<T> : IComparer<ItemDependencyInfo<T>> where T : class
        {
            private readonly StringComparer _comparer;

            public PackageInfoComparer(StringComparer comparer)
            {
                _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            }

            public int Compare(ItemDependencyInfo<T>? x, ItemDependencyInfo<T>? y)
            {
                // Order packages by parent count
                if (x!.ActiveParents < y!.ActiveParents)
                {
                    return -1;
                }
                if (x.ActiveParents > y.ActiveParents)
                {
                    return 1;
                }

                return _comparer.Compare(x.Id, y.Id);
            }
        }

        [DebuggerDisplay("{Package.Id} Active: {ActiveParents}")]
        private sealed class ItemDependencyInfo<T> where T : class
        {
            public ItemDependencyInfo(string id, string[] dependencyIds, T item)
            {
                ActiveParents = 0;
                Id = id;
                DependencyIds = dependencyIds;
                Item = item;
            }

            public string Id;
            public string[] DependencyIds;
            public T Item;

            public int ActiveParents;
            public List<ItemDependencyInfo<T>>? Children;
        }
    }
}
