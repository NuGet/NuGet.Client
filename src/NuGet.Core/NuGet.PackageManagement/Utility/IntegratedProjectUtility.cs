// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public static class IntegratedProjectUtility
    {
        /// <summary>
        /// Gets all package dependencies in a project either sorted or not.
        /// Project must be restored.
        /// </summary>
        public static async Task<IReadOnlyList<PackageIdentity>> GetProjectPackageDependenciesAsync(
            INuGetIntegratedProject integratedProject, bool sorted)
        {
            var lockFile = await GetLockFileOrNull(integratedProject);

            if (lockFile != null)
            {
                return GetLockFilePackageDependencies(lockFile, sorted);
            }

            return new List<PackageIdentity>();
        }

        /// <summary>
        /// Read lock file
        /// </summary>
        public static async Task<LockFile> GetLockFileOrNull(INuGetIntegratedProject integratedProject)
        {
            var lockFilePath = await integratedProject.GetAssetsFilePathOrNullAsync();
            
            if (lockFilePath == null)
            {
                return null;
            }

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
        public static IReadOnlyList<PackageIdentity> GetLockFilePackageDependencies(LockFile lockFile, bool sorted)
        {
            IReadOnlyList<LibraryIdentity> dependencies;
            if (sorted)
            {
                dependencies = GetOrderedLockFileDependencies(lockFile);
            }
            else
            {
                dependencies = GetLockFileDependencies(lockFile);
            }

            return dependencies
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version))
                .ToList();
        }

        /// <summary>
        /// Get dependencies from the lock file
        /// </summary>
        /// <param name="lockFile"></param>
        /// <returns></returns>
        public static IReadOnlyList<LibraryIdentity> GetLockFileDependencies(LockFile lockFile)
        {
            var results = new List<LibraryIdentity>();
            var typeMappings = new Dictionary<PackageDependencyInfo, LibraryIdentity>(PackageIdentity.Comparer);
       
            foreach (var target in lockFile.Targets)
            {
                foreach (var targetLibrary in target.Libraries)
                {
                    var identity = new PackageIdentity(targetLibrary.Name, targetLibrary.Version);
                    var dependency = new PackageDependencyInfo(identity, targetLibrary.Dependencies);

                    if (!typeMappings.ContainsKey(dependency))
                    {
                        var libraryIdentity = new LibraryIdentity(
                            targetLibrary.Name,
                            targetLibrary.Version,
                            LibraryType.Parse(targetLibrary.Type));

                        results.Add(libraryIdentity);
                        typeMappings.Add(dependency, libraryIdentity);
                    }
                }
            }

            return results;
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
            var sortedDependencies = TopologicalSortUtility.SortPackagesByDependencyOrder(dependencies);

            foreach (var dependency in sortedDependencies)
            {
                // Convert back
                // PackageDependencyInfo -> LibraryIdentity
                results.Add(typeMappings[dependency]);
            }

            return results;
        }
    }
}
