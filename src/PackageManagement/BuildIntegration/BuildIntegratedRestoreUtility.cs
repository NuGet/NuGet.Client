// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling the RestoreCommand
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            Logging.ILogger logger,
            IEnumerable<string> sources,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            var globalPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            return await RestoreAsync(project, logger, sources, globalPath, token);
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            Logging.ILogger logger,
            IEnumerable<string> sources,
            string globalPackagesFolderPath,
            CancellationToken token)
        {
            // Restore
            var result = await RestoreAsync(project, project.PackageSpec, logger, sources, globalPackagesFolderPath, token);

            // Throw before writing if this has been canceled
            token.ThrowIfCancellationRequested();

            // Write out the lock file and msbuild files
            result.Commit(logger);

            return result;
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            Logging.ILogger logger,
            IEnumerable<string> sources,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            var globalPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            return await RestoreAsync(project, packageSpec, logger, sources, globalPath, token);
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            Logging.ILogger logger,
            IEnumerable<string> sources,
            string globalPackageFolderPath,
            CancellationToken token)
        {
            // Restoring packages
            logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            var packageSources = sources.Select(source => new Configuration.PackageSource(source));
            var request = new RestoreRequest(packageSpec, packageSources, globalPackageFolderPath);
            request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;

            // Find the full closure of project.json files and referenced projects
            var projectReferences = await project.GetProjectReferenceClosureAsync();
            request.ExternalProjects = projectReferences
                .Where(reference => !string.IsNullOrEmpty(reference.PackageSpecPath))
                .Select(reference => BuildIntegratedProjectUtility.ConvertProjectReference(reference))
                .ToList();

            token.ThrowIfCancellationRequested();

            var command = new RestoreCommand(logger, request);

            // Execute the restore
            var result = await command.ExecuteAsync(token);

            // Report a final message with the Success result
            if (result.Success)
            {
                logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.BuildIntegratedPackageRestoreSucceeded,
                    project.ProjectName));
            }
            else
            {
                logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.BuildIntegratedPackageRestoreFailed,
                    project.ProjectName));
            }

            return result;
        }

        /// <summary>
        /// Find all packages added to <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetAddedPackages(LockFile originalLockFile, LockFile updatedLockFile)
        {
            var updatedPackages = updatedLockFile.Targets.SelectMany(target => target.Libraries)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            var originalPackages = originalLockFile.Targets.SelectMany(target => target.Libraries)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            var results = updatedPackages.Except(originalPackages, PackageIdentity.Comparer).ToList();

            return results;
        }

        /// <summary>
        /// Find all packages removed from <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetRemovedPackages(LockFile originalLockFile, LockFile updatedLockFile)
        {
            return GetAddedPackages(updatedLockFile, originalLockFile);
        }

        /// <summary>
        /// Creates an index of the project unique name to the cache entry. 
        /// The cache entry contains the project and the closure of project.json files.
        /// </summary>
        public static async Task<Dictionary<string, BuildIntegratedProjectCacheEntry>>
            CreateBuildIntegratedProjectStateCache(IReadOnlyList<BuildIntegratedNuGetProject> projects)
        {
            var cache = new Dictionary<string, BuildIntegratedProjectCacheEntry>();

            // Find all project closures
            foreach (var project in projects)
            {
                // Get all project.json file paths in the closure
                var closure = await project.GetProjectReferenceClosureAsync();
                var files = closure.Select(reference => reference.PackageSpecPath).ToList();

                var projectInfo = new BuildIntegratedProjectCacheEntry(
                    project.JsonConfigPath,
                    files,
                    project.PackageSpec.RuntimeGraph.Supports.Keys.OrderBy(p => p, StringComparer.Ordinal).ToArray());

                var uniqueName = project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);

                if (!cache.ContainsKey(uniqueName))
                {
                    cache.Add(uniqueName, projectInfo);
                }
                else
                {
                    Debug.Fail("project list contains duplicate projects");
                }
            }

            return cache;
        }

        /// <summary>
        /// Verifies that the caches contain the same projects and that each project contains the same closure.
        /// This is used to detect if any projects have changed before verifying the lock files.
        /// </summary>
        public static bool CacheHasChanges(
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> previousCache,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> currentCache)
        {
            foreach (var item in currentCache)
            {
                var projectName = item.Key;
                BuildIntegratedProjectCacheEntry projectInfo;
                if (!previousCache.TryGetValue(projectName, out projectInfo))
                {
                    // A new project was added, this needs a restore
                    return true;
                }

                if (!item.Value.PackageSpecClosure.OrderBy(s => s)
                    .SequenceEqual(projectInfo.PackageSpecClosure.OrderBy(s => s)))
                {
                    // The project closure has changed
                    return true;
                }

                if (!Enumerable.SequenceEqual(item.Value.SupportsProfiles, projectInfo.SupportsProfiles))
                {
                    // Supports nodes have changes. Compatibility checks need to be done during the restore.
                    return true;
                }
            }

            // no project changes have occurred
            return false;
        }

        /// <summary>
        /// Validate that all project.lock.json files are validate for the project.json files, and that no packages are missing.
        /// If a full restore is required this will return false.
        /// </summary>
        /// <remarks>Floating versions and project.json files with supports require a full restore.</remarks>
        public static bool IsRestoreRequired(IReadOnlyList<BuildIntegratedNuGetProject> projects, VersionFolderPathResolver pathResolver)
        {
            var hashesChecked = new HashSet<string>();

            var packageSpecs = new Dictionary<string, PackageSpec>();

            // Load all package specs and validate them first for floating versions and supports.
            foreach (var project in projects)
            {
                var path = project.JsonConfigPath;

                if (!packageSpecs.ContainsKey(path))
                {
                    var packageSpec = project.PackageSpec;

                    if (packageSpec.Dependencies.Any(dependency => dependency.LibraryRange.VersionRange.IsFloating))
                    {
                        // Floating dependencies need to be checked each time
                        return true;
                    }

                    packageSpecs.Add(path, packageSpec);
                }
            }

            // Validate project.lock.json files
            foreach (var project in projects)
            {
                var packageSpec = packageSpecs[project.JsonConfigPath];

                var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(project.JsonConfigPath);

                if (!File.Exists(lockFilePath))
                {
                    // If the lock file does not exist a restore is needed
                    return true;
                }

                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Read(lockFilePath);

                if (!lockFile.IsValidForPackageSpec(packageSpec))
                {
                    // The project.json file has been changed and the lock file needs to be updated.
                    return true;
                }

                // Verify all libraries are on disk
                foreach (var library in lockFile.Libraries)
                {
                    // Verify the SHA for each package
                    var hashPath = pathResolver.GetHashPath(library.Name, library.Version);

                    // Libraries shared between projects can be skipped
                    if (hashesChecked.Add(hashPath))
                    {
                        if (File.Exists(hashPath))
                        {
                            var sha512 = File.ReadAllText(hashPath);

                            if (library.Sha512 != sha512)
                            {
                                // A package has changed
                                return true;
                            }
                        }
                        else
                        {
                            // A package is missing
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
