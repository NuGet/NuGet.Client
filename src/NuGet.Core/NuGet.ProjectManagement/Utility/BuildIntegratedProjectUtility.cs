// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Utilities for project.json
    /// </summary>
    public static class BuildIntegratedProjectUtility
    {
        /// <summary>
        /// Get the root path of a package from the global folder.
        /// </summary>
        public static string GetPackagePathFromGlobalSource(
            string effectiveGlobalPackagesFolder,
            PackageIdentity identity)
        {
            var pathResolver = new VersionFolderPathResolver(effectiveGlobalPackagesFolder);
            return pathResolver.GetInstallPath(identity.Id, identity.Version);
        }

        public static IReadOnlyList<PackageIdentity> GetOrderedProjectDependencies(
            BuildIntegratedNuGetProject buildIntegratedProject)
        {
            var results = new List<PackageIdentity>();

            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);
            var lockFileFormat = new LockFileFormat();

            // Read the lock file to find the full closure of dependencies
            if (File.Exists(lockFilePath))
            {
                var lockFile = lockFileFormat.Read(lockFilePath);

                var dependencies = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

                foreach (var target in lockFile.Targets)
                {
                    foreach (var targetLibrary in target.Libraries)
                    {
                        var identity = new PackageIdentity(targetLibrary.Name, targetLibrary.Version);
                        var dependency = new PackageDependencyInfo(identity, targetLibrary.Dependencies);
                        dependencies.Add(dependency);
                    }
                }

                // Sort dependencies
                var sortedDependencies = SortPackagesByDependencyOrder(dependencies);
                results.AddRange(sortedDependencies);
            }

            return results;
        }

        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        private static IReadOnlyList<PackageDependencyInfo> SortPackagesByDependencyOrder(
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
            int count = 0;

            foreach (var package in packages)
            {
                if (package.Dependencies != null
                    && package.Dependencies.Any(dependency =>
                        string.Equals(id, dependency.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
