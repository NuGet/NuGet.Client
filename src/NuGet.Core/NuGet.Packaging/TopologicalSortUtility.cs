﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class TopologicalSortUtility
    {
        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        public static IReadOnlyList<PackageDependencyInfo> SortPackagesByDependencyOrder(
            IEnumerable<PackageDependencyInfo> packages)
        {
            var sorted = new List<PackageDependencyInfo>();
            var toSort = packages.Distinct().ToList();

            while (toSort.Count > 0)
            {
                // Order packages by parent count, take the child with the lowest number of parents
                // and remove it from the list
                var nextPackage = toSort.OrderBy(package => GetParentCount(toSort, package.Id))
                    .ThenBy(package => package.Id, StringComparer.OrdinalIgnoreCase).First();

                sorted.Add(nextPackage);
                toSort.Remove(nextPackage);
            }

            // the list is ordered by parents first, reverse to run children first
            sorted.Reverse();

            return sorted;
        }

        private static int GetParentCount(List<PackageDependencyInfo> packages, string id)
        {
            var parentCount = 0;
            var count = packages.Count;

            for (var i = 0; i < count; i++)
            {
                var deps = packages[i].Dependencies;
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
    }
}
