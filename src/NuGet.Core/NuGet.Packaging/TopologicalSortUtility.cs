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
        private static readonly Comparer _comparer = new Comparer();
        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        public static IReadOnlyList<PackageDependencyInfo> SortPackagesByDependencyOrder(
            IEnumerable<PackageDependencyInfo> packages)
        {
            var toSort = packages.Distinct().Select(package => new PackageInfo(package)).ToArray();
            var sorted = new List<PackageDependencyInfo>(toSort.Length);

            for (var i = 0; i < toSort.Length; i++)
            {
                Sort(toSort, i);
                // take the child with the lowest number of parents
                sorted.Add(toSort[i].Package);
            }

            // the list is ordered by parents first, reverse to run children first
            sorted.Reverse();

            return sorted;
        }

        private static void Sort(PackageInfo[] packages, int start)
        {
            // Update parent counts
            for (var i = start; i < packages.Length; i++)
            {
                var package = packages[i].Package;
                // Mutating stuct in-place, need to index rather than use enumerator
                packages[i].ParentCount = GetParentCount(packages, package.Id, start);
            }

            Array.Sort(packages, 0, 0, _comparer);
        }

        private static int GetParentCount(PackageInfo[] packages, string id, int start)
        {
            var parentCount = 0;

            for (var i = start; i < packages.Length; i++)
            {
                var deps = packages[i].Package.Dependencies;
                var dependencies = deps as PackageDependency[] ?? deps.ToArray();

                foreach (var dependency in dependencies)
                {
                    if (string.Equals(id, dependency.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        parentCount++;
                        break;
                    }
                }
            }

            return parentCount;
        }

        private class Comparer : IComparer<PackageInfo>
        {
            public int Compare(PackageInfo x, PackageInfo y)
            {
                // Order packages by parent count
                if (x.ParentCount < y.ParentCount)
                {
                    return -1;
                }
                if (x.ParentCount > y.ParentCount)
                {
                    return 1;
                }
                return StringComparer.OrdinalIgnoreCase.Compare(x.Package.Id, y.Package.Id);
            }
        }

        private struct PackageInfo
        {
            public PackageInfo(PackageDependencyInfo package)
            {
                Package = package;
                ParentCount = 0;
            }

            public PackageDependencyInfo Package;
            public int ParentCount;
        }
    }
}
