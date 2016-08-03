// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.LibraryModel;
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

        /// <summary>
        /// Orders all package dependencies in a project.
        /// Project must be restored.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetOrderedProjectPackageDependencies(
            BuildIntegratedNuGetProject buildIntegratedProject)
        {
            var lockFile = GetLockFileOrNull(buildIntegratedProject);

            if (lockFile != null)
            {
                return GetOrderedLockFilePackageDependencies(lockFile);
            }

            return new List<PackageIdentity>();
        }

        /// <summary>
        /// Orders all dependencies in a project. This includes both packages and projects.
        /// Project must be restored.
        /// </summary>
        public static IReadOnlyList<LibraryIdentity> GetOrderedProjectDependencies(
            BuildIntegratedNuGetProject buildIntegratedProject)
        {
            var lockFile = GetLockFileOrNull(buildIntegratedProject);

            if (lockFile != null)
            {
                return GetOrderedLockFileDependencies(lockFile);
            }

            return new List<LibraryIdentity>();
        }

        /// <summary>
        /// Read lock file
        /// </summary>
        public static LockFile GetLockFileOrNull(BuildIntegratedNuGetProject buildIntegratedProject)
        {
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);
            return GetLockFileOrNull(lockFilePath);
        }

        /// <summary>
        /// Read lock file
        /// </summary>
        public static LockFile GetLockFileOrNull(string lockFilePath)
        {
            LockFile lockFile = null;
            var lockFileFormat = new LockFileFormat();

            // Read the lock file to find the full closure of dependencies
            if (File.Exists(lockFilePath))
            {
                lockFile = lockFileFormat.Read(lockFilePath);
            }

            return lockFile;
        }

        /// <summary>
        /// Lock file dependencies - packages only
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetOrderedLockFilePackageDependencies(LockFile lockFile)
        {
            return GetOrderedLockFileDependencies(lockFile)
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version))
                .ToList();
        }

        /// <summary>
        /// Get ordered dependencies from the lock file
        /// </summary>
        /// <param name="lockFile"></param>
        /// <returns></returns>
        public static IReadOnlyList<LibraryIdentity> GetOrderedLockFileDependencies(LockFile lockFile)
        {
            var results = new List<LibraryIdentity>();

            var dependencies = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);
            var typeMappings = new Dictionary<PackageDependencyInfo, LibraryIdentity>(PackageIdentity.Comparer);

            foreach (var target in lockFile.Targets)
            {
                foreach (var targetLibrary in target.Libraries)
                {
                    var identity = new PackageIdentity(targetLibrary.Name, targetLibrary.Version);
                    var dependency = new PackageDependencyInfo(identity, targetLibrary.Dependencies);
                    dependencies.Add(dependency);

                    if (!typeMappings.ContainsKey(dependency))
                    {
                        var libraryIdentity = new LibraryIdentity(
                            targetLibrary.Name,
                            targetLibrary.Version,
                            LibraryType.Parse(targetLibrary.Type));

                        typeMappings.Add(dependency, libraryIdentity);
                    }
                }
            }

            // Sort dependencies
            var sortedDependencies = SortPackagesByDependencyOrder(dependencies);

            foreach (var dependency in sortedDependencies)
            {
                // Convert back
                // PackageDependencyInfo -> LibraryIdentity
                results.Add(typeMappings[dependency]);
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
