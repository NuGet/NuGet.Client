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
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

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
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            CancellationToken token)
        {
            return await RestoreAsync(
                project,
                logger,
                sources,
                effectiveGlobalPackagesFolder,
                c => { },
                token);
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            Logging.ILogger logger,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            Action<SourceCacheContext> cacheContextModifier,
            CancellationToken token)
        {
            // Restore
            var result = await RestoreAsync(
                project,
                project.PackageSpec,
                logger,
                sources,
                effectiveGlobalPackagesFolder,
                cacheContextModifier,
                token);

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
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            Action<SourceCacheContext> cacheContextModifier,
            CancellationToken token)
        {
            // Restoring packages
            logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            using (var request = new RestoreRequest(packageSpec, sources, effectiveGlobalPackagesFolder))
            {
                request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;

                if (cacheContextModifier != null)
                {
                    cacheContextModifier(request.CacheContext);
                }

                // Add the existing lock file if it exists
                var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(project.JsonConfigPath);
                request.LockFilePath = lockFilePath;
                request.ExistingLockFile = GetLockFile(lockFilePath, logger);

                // Find the full closure of project.json files and referenced projects
                var projectReferences = await project.GetProjectReferenceClosureAsync(logger);
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
        }

        /// <summary>
        /// Find all packages added to <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetAddedPackages(
            LockFile originalLockFile,
            LockFile updatedLockFile)
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
        public static IReadOnlyList<PackageIdentity> GetRemovedPackages(
            LockFile originalLockFile,
            LockFile updatedLockFile)
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
        /// Validate that all project.lock.json files are validate for the project.json files,
        /// and that no packages are missing.
        /// If a full restore is required this will return false.
        /// </summary>
        /// <remarks>Floating versions and project.json files with supports require a full restore.</remarks>
        public static bool IsRestoreRequired(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            VersionFolderPathResolver pathResolver)
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

        /// <summary>
        /// Find the list of parent projects which directly or indirectly reference the child project.
        /// </summary>
        public static async Task<IReadOnlyList<BuildIntegratedNuGetProject>> GetParentProjectsInClosure(
            ISolutionManager solutionManager,
            BuildIntegratedNuGetProject target)
        {
            var projects = solutionManager.GetNuGetProjects().OfType<BuildIntegratedNuGetProject>().ToList();

            return await GetParentProjectsInClosure(projects, target);
        }

        /// <summary>
        /// Find the list of parent projects which directly or indirectly reference the child project.
        /// </summary>
        public static async Task<IReadOnlyList<BuildIntegratedNuGetProject>> GetParentProjectsInClosure(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            BuildIntegratedNuGetProject target)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var parents = new HashSet<BuildIntegratedNuGetProject>();

            var targetProjectJson = Path.GetFullPath(target.JsonConfigPath);

            foreach (var project in projects)
            {
                // do not count the target as a parent
                if (!target.Equals(project))
                {
                    var closure = await project.GetProjectReferenceClosureAsync();

                    // find all projects which have a child reference matching the same project.json path as the target
                    if (closure.Any(reference =>
                        !string.IsNullOrEmpty(reference.PackageSpecPath) &&
                        string.Equals(targetProjectJson,
                            Path.GetFullPath(reference.PackageSpecPath),
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        parents.Add(project);
                    }
                }
            }

            // sort parents by name to make this more deterministic during restores
            return parents.OrderBy(parent => parent.ProjectName).ToList();
        }

        /// <summary>
        /// Returns the lockfile if it exists, otherwise null.
        /// </summary>
        public static LockFile GetLockFile(string lockFilePath, Logging.ILogger logger)
        {
            LockFile lockFile = null;

            if (File.Exists(lockFilePath))
            {
                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                lockFile = format.Read(lockFilePath, logger);
            }

            return lockFile;
        }
    }
}
