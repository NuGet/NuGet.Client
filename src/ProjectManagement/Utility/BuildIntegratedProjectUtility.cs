// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        /// project.json
        /// </summary>
        public static readonly string ProjectConfigFileName = "project.json";

        /// <summary>
        /// .project.json
        /// </summary>
        public static readonly string ProjectConfigFileEnding = ".project.json";

        /// <summary>
        /// Lock file name
        /// </summary>
        public static readonly string ProjectLockFileName = "project.lock.json";

        public static string GetEffectiveGlobalPackagesFolder(string solutionDirectory, ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
            if (Path.IsPathRooted(globalPackagesFolder))
            {
                return globalPackagesFolder;
            }

            if (string.IsNullOrEmpty(solutionDirectory))
            {
                throw new ArgumentNullException(nameof(solutionDirectory));
            }

            if (!Path.IsPathRooted(solutionDirectory))
            {
                throw new ArgumentException(Strings.SolutionDirectoryMustBeRooted);
            }

            return Path.GetFullPath(Path.Combine(solutionDirectory, globalPackagesFolder));
        }

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
        /// Create the lock file path from the config file path.
        /// If the config file includes a project name the 
        /// lock file will include the name also.
        /// </summary>
        public static string GetLockFilePath(string configFilePath)
        {
            string lockFilePath = null;

            var dir = Path.GetDirectoryName(configFilePath);

            var projectName = GetProjectNameFromConfigFileName(configFilePath);

            if (projectName == null)
            {
                lockFilePath = Path.Combine(dir, ProjectLockFileName);
            }
            else
            {
                var lockFileWithProject = GetProjectLockFileNameWithProjectName(projectName);
                lockFilePath = Path.Combine(dir, lockFileWithProject);
            }

            return lockFilePath;
        }

        /// <summary>
        /// Creates a projectName.project.json file name.
        /// </summary>
        public static string GetProjectConfigWithProjectName(string projectName)
        {
            if (String.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", projectName, ProjectConfigFileName);
        }

        /// <summary>
        /// Creates a projectName.project.lock.json file name.
        /// </summary>
        public static string GetProjectLockFileNameWithProjectName(string projectName)
        {
            if (String.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", projectName, ProjectLockFileName);
        }

        /// <summary>
        /// Parses a projectName.project.json file name into a project name.
        /// If there is no project name null will be returned.
        /// </summary>
        public static string GetProjectNameFromConfigFileName(string configPath)
        {
            if (configPath == null)
            {
                throw new ArgumentNullException(nameof(configPath));
            }

            var file = Path.GetFileName(configPath);

            string projectName = null;

            if (file != null && file.EndsWith(ProjectConfigFileEnding, StringComparison.OrdinalIgnoreCase))
            {
                var prefixLength = file.Length - ProjectConfigFileName.Length - 1;
                projectName = file.Substring(0, prefixLength);
            }

            return projectName;
        }

        /// <summary>
        /// True if the file is a project.json or projectname.project.json file.
        /// </summary>
        public static bool IsProjectConfig(string configPath)
        {
            if (configPath == null)
            {
                throw new ArgumentNullException(nameof(configPath));
            }

            if (configPath.EndsWith(ProjectConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                string file = null;

                try
                {
                    file = Path.GetFileName(configPath);
                }
                catch
                {
                    // ignore invalid paths
                    return false;
                }

                return string.Equals(ProjectConfigFileName, file, StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(ProjectConfigFileEnding, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Finds the projectName.project.json in a directory. If no projectName.project.json exists
        /// the default project.json path will be returned regardless of existance.
        /// </summary>
        /// <returns>Returns the full path to the project.json file.</returns>
        public static string GetProjectConfigPath(string directoryPath, string projectName)
        {
            if (String.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty);
            }

            // Check for the project name based file first
            var configPath = Path.Combine(directoryPath, GetProjectConfigWithProjectName(projectName));

            if (!File.Exists(configPath))
            {
                // Fallback to project.json
                configPath = Path.Combine(directoryPath, ProjectConfigFileName);
            }

            return configPath;
        }

        /// <summary>
        /// BuildIntegratedProjectReference -> ExternalProjectReference
        /// </summary>
        public static ExternalProjectReference ConvertProjectReference(BuildIntegratedProjectReference reference)
        {
            return new ExternalProjectReference(
                reference.Name,
                reference.PackageSpecPath,
                reference.ExternalProjectReferences.Where(externalReference =>
                    !externalReference.Equals(reference.Name, StringComparison.OrdinalIgnoreCase)));
        }

        public static IReadOnlyList<PackageIdentity> GetOrderedProjectDependencies(
            BuildIntegratedNuGetProject buildIntegratedProject)
        {
            var results = new List<PackageIdentity>();

            var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);
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
