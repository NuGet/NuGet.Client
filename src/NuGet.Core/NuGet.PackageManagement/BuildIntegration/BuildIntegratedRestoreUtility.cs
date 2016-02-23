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
            ExternalProjectReferenceContext context,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            CancellationToken token)
        {
            return await RestoreAsync(
                project,
                context,
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
            ExternalProjectReferenceContext context,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            Action<SourceCacheContext> cacheContextModifier,
            CancellationToken token)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContextModifier(cacheContext);

                var providers = RestoreCommandProviders.Create(effectiveGlobalPackagesFolder,
                    sources,
                    cacheContext,
                    context.Logger);

                // Restore
                var result = await RestoreAsync(
                    project,
                    project.PackageSpec,
                    context,
                    providers,
                    token);

                // Throw before writing if this has been canceled
                token.ThrowIfCancellationRequested();

                // Write out the lock file and msbuild files
                result.Commit(context.Logger);

                return result;
            }
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            ExternalProjectReferenceContext context,
            RestoreCommandProviders providers,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;
            logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            using (var request = new RestoreRequest(packageSpec, providers, logger, disposeProviders: false))
            {
                request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;
                request.LockFileVersion = await GetLockFileVersion(project, context);

                // Add the existing lock file if it exists
                var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(project.JsonConfigPath);
                request.LockFilePath = lockFilePath;
                request.ExistingLockFile = GetLockFile(lockFilePath, logger);

                // Find the full closure of project.json files and referenced projects
                var projectReferences = await project.GetProjectReferenceClosureAsync(context);
                request.ExternalProjects = projectReferences.ToList();

                token.ThrowIfCancellationRequested();

                var command = new RestoreCommand(request);

                // Execute the restore
                var result = await command.ExecuteAsync(token);

                // Report a final message with the Success result
                if (result.Success)
                {
                    logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                        Strings.BuildIntegratedPackageRestoreSucceeded,
                        project.ProjectName));
                }
                else
                {
                    logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
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
            CreateBuildIntegratedProjectStateCache(
                IReadOnlyList<BuildIntegratedNuGetProject> projects,
                ExternalProjectReferenceContext context)
        {
            var cache = new Dictionary<string, BuildIntegratedProjectCacheEntry>();

            // Find all project closures
            foreach (var project in projects)
            {
                // Get all project.json file paths in the closure
                var closure = await project.GetProjectReferenceClosureAsync(context);

                var files = new HashSet<string>(StringComparer.Ordinal);

                // Store the last modified date of the project.json file
                // If there are any changes a restore is needed
                var lastModified = DateTimeOffset.MinValue;

                if (File.Exists(project.JsonConfigPath))
                {
                    lastModified = File.GetLastWriteTimeUtc(project.JsonConfigPath);
                }

                foreach (var reference in closure)
                {
                    if (!string.IsNullOrEmpty(reference.MSBuildProjectPath))
                    {
                        files.Add(reference.MSBuildProjectPath);
                    }

                    if (reference.PackageSpecPath != null)
                    {
                        files.Add(reference.PackageSpecPath);
                    }
                }

                var projectInfo = new BuildIntegratedProjectCacheEntry(
                    project.JsonConfigPath,
                    files,
                    lastModified);

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

                if (item.Value.ProjectConfigLastModified?.Equals(projectInfo.ProjectConfigLastModified) != true)
                {
                    // project.json has been modified
                    return true;
                }

                if (!item.Value.ReferenceClosure.SetEquals(projectInfo.ReferenceClosure))
                {
                    // The project closure has changed
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
        public static async Task<bool> IsRestoreRequired(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            VersionFolderPathResolver pathResolver,
            ExternalProjectReferenceContext referenceContext)
        {
            var hashesChecked = new HashSet<string>();

            // Validate project.lock.json files
            foreach (var project in projects)
            {
                var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(project.JsonConfigPath);

                if (!File.Exists(lockFilePath))
                {
                    // If the lock file does not exist a restore is needed
                    return true;
                }

                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Read(lockFilePath, referenceContext.Logger);

                var lockFileVersion = await GetLockFileVersion(project, referenceContext);

                var packageSpec = referenceContext.GetOrCreateSpec(project.ProjectName, project.JsonConfigPath);

                if (!lockFile.IsValidForPackageSpec(packageSpec, lockFileVersion))
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
            BuildIntegratedNuGetProject target,
            ExternalProjectReferenceContext referenceContext)
        {
            var projects = solutionManager.GetNuGetProjects().OfType<BuildIntegratedNuGetProject>().ToList();

            return await GetParentProjectsInClosure(projects, target, referenceContext);
        }

        /// <summary>
        /// Find the list of parent projects which directly or indirectly reference the child project.
        /// </summary>
        public static async Task<IReadOnlyList<BuildIntegratedNuGetProject>> GetParentProjectsInClosure(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            BuildIntegratedNuGetProject target,
            ExternalProjectReferenceContext referenceContext)
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
                    var closure = await project.GetProjectReferenceClosureAsync(referenceContext);

                    // find all projects which have a child reference matching the same project.json path as the target
                    if (closure.Any(reference =>
                        reference.PackageSpecPath != null &&
                        string.Equals(targetProjectJson,
                            Path.GetFullPath(reference.PackageSpecPath),
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        parents.Add(project);
                    }
                }
            }

            // sort parents by name to make this more deterministic during restores
            return parents.OrderBy(parent => parent.ProjectName, StringComparer.Ordinal).ToList();
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

        /// <summary>
        /// If the project is non-xproj and has no xproj references it may fallback to v1.
        /// </summary>
        public static async Task<int> GetLockFileVersion(
            NuGetProject project,
            ExternalProjectReferenceContext referenceContext)
        {
            var lockFileVersion = LockFileFormat.Version;

            var buildProject = project as BuildIntegratedNuGetProject;

            if (buildProject != null)
            {
                var references = await buildProject.GetProjectReferenceClosureAsync(referenceContext);

                lockFileVersion = GetLockFileVersion(references);
            }

            return lockFileVersion;
        }

        // MSBuild for VS2015U1 fails when projects are in the lock file since it treats them as packages.
        // To work around that NuGet will downgrade the lock file if there are only csproj references.
        // Projects with zero project references can go to v2, and projects with xproj references must be
        // at least v2 to work.
        // references should include the parent project
        public static int GetLockFileVersion(IReadOnlyList<ExternalProjectReference> references)
        {
            var version = LockFileFormat.Version;

            // if xproj is used the higher version must be used
            if (references.Any(reference => reference.ExternalProjectReferences.Count > 0)
                && !references.Any(reference =>
                        reference.MSBuildProjectPath?.EndsWith(XProjUtility.XProjExtension) == true))
            {
                // Fallback to v1 for non-xprojs with p2ps
                version = 1;
            }

            return version;
        }

        /// <summary>
        /// Find the project closure from a larger set of references.
        /// </summary>
        public static ISet<ExternalProjectReference> GetExternalClosure(
            string rootUniqueName,
            ISet<ExternalProjectReference> references)
        {
            var closure = new HashSet<ExternalProjectReference>();

            // Start with the parent node
            var parent = references.FirstOrDefault(project =>
                    rootUniqueName.Equals(project.UniqueName, StringComparison.Ordinal));

            if (parent != null)
            {
                closure.Add(parent);
            }

            // Loop adding child projects each time
            var notDone = true;
            while (notDone)
            {
                notDone = false;

                foreach (var childName in closure
                    .Where(project => project.ExternalProjectReferences != null)
                    .SelectMany(project => project.ExternalProjectReferences)
                    .ToArray())
                {
                    var child = references.FirstOrDefault(project =>
                        childName.Equals(project.UniqueName, StringComparison.Ordinal));

                    // Continue until nothing new is added
                    if (child != null)
                    {
                        notDone |= closure.Add(child);
                    }
                }
            }

            return closure;
        }
    }
}
