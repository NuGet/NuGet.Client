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
using NuGet.Common;
using NuGet.LibraryModel;
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
            IEnumerable<string> fallbackPackageFolders,
            CancellationToken token)
        {
            return await RestoreAsync(
                project,
                context,
                sources,
                effectiveGlobalPackagesFolder,
                fallbackPackageFolders,
                c => { },
                token);
        }

        public static async Task ExecuteInitPs1ScriptsAsync(
            BuildIntegratedNuGetProject project,
            IEnumerable<PackageIdentity> packages,
            FallbackPackagePathResolver pathResolver,
            INuGetProjectContext projectContext)
        {
            // Find all dependencies in sorted order
            var sortedPackages = BuildIntegratedProjectUtility.GetOrderedProjectPackageDependencies(project);

            // Keep track of the packages that need to be executed.
            var packagesToExecute = new HashSet<PackageIdentity>(packages, PackageIdentity.Comparer);

            // Use the ordered packages to run init.ps1 for the specified packages.
            foreach (var package in sortedPackages)
            {
                if (packagesToExecute.Remove(package))
                {
                    var packageInstallPath = pathResolver.GetPackageDirectory(package.Id, package.Version);
                    
                    if (packageInstallPath == null)
                    {
                        continue;
                    }

                    await project.ExecuteInitScriptAsync(
                        package,
                        packageInstallPath,
                        projectContext,
                        throwOnFailure: false);
                }
            }
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            ExternalProjectReferenceContext context,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            IEnumerable<string> fallbackPackageFolders,
            Action<SourceCacheContext> cacheContextModifier,
            CancellationToken token)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContextModifier(cacheContext);

                var providers = RestoreCommandProviders.Create(effectiveGlobalPackagesFolder,
                    fallbackPackageFolders,
                    sources,
                    cacheContext,
                    context.Logger);

                // Restore
                var result = await RestoreAsync(
                    project,
                    project.PackageSpec,
                    context,
                    providers,
                    cacheContext,
                    token);

                // Throw before writing if this has been canceled
                token.ThrowIfCancellationRequested();

                // Write out the lock file and msbuild files
                await result.CommitAsync(context.Logger, token);

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
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;
            logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            var request = new RestoreRequest(packageSpec, providers, cacheContext, logger);
            request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;

            // Add the existing lock file if it exists
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(project.JsonConfigPath);
            request.LockFilePath = lockFilePath;
            request.ExistingLockFile = LockFileUtilities.GetLockFile(lockFilePath, logger);

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

        /// <summary>
        /// Determine what packages need to have their init.ps1 scripts executed based on the provided restore result.
        /// When a restore happens, new packages can be introduced (because the project.json was updated since the last
        /// restore) or existing packages can be installed (because the global packages folder was cleared or a restore
        /// has never been run on an existing project.json). In both of these cases, the init.ps1 scripts should be
        /// executed. Also, the init.ps1 scripts should be executed in dependency order, however it is the
        /// resposibility of <see cref="ExecuteInitPs1ScriptsAsync(BuildIntegratedNuGetProject, IEnumerable{PackageIdentity}, FallbackPackagePathResolver, INuGetProjectContext)"/>
        /// to do this.
        /// </summary>
        /// <param name="restoreResult">The restore result to examine.</param>
        /// <returns>The packages to execute init.ps1 scripts.</returns>
        public static IReadOnlyList<PackageIdentity> GetPackagesToExecuteInitPs1(RestoreResult restoreResult)
        {
            Debug.Assert(restoreResult.Success, "We should not be executing init.ps1 scripts after a failed restore.");

            // Packages added from the previous restore.
            var addedPackages = GetAddedPackages(restoreResult.PreviousLockFile, restoreResult.LockFile);
            
            // Packages that were not installed before.
            var installedPackages = restoreResult
                .GetAllInstalled()
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            // Get unique package identities.
            var newPackages = new HashSet<PackageIdentity>(addedPackages.Concat(installedPackages));
            
            return newPackages.ToList();
        }

        /// <summary>
        /// Find all packages added to <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetAddedPackages(
            LockFile originalLockFile,
            LockFile updatedLockFile)
        {
            IEnumerable<PackageIdentity> updatedPackages;
            if (updatedLockFile != null)
            {
                updatedPackages = updatedLockFile
                    .Targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .Select(library => new PackageIdentity(library.Name, library.Version));
            }
            else
            {
                updatedPackages = Enumerable.Empty<PackageIdentity>();
            }

            IEnumerable<PackageIdentity> originalPackages;
            if (originalLockFile != null)
            {
                originalPackages = originalLockFile
                    .Targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .Select(library => new PackageIdentity(library.Name, library.Version));
            }
            else
            {
                originalPackages = Enumerable.Empty<PackageIdentity>();
            }

            var results = updatedPackages
                .Except(originalPackages, PackageIdentity.Comparer)
                .ToList();

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

                var uniqueName = string.Empty;

                if (!project.TryGetMetadata(NuGetProjectMetadataKeys.UniqueName, out uniqueName))
                {
                    uniqueName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                }

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
        public static bool IsRestoreRequired(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            IReadOnlyList<string> packageFolderPaths,
            ExternalProjectReferenceContext referenceContext)
        {
            var packagesChecked = new HashSet<PackageIdentity>();
            var pathResolvers = packageFolderPaths.Select(path => new VersionFolderPathResolver(path));

            // Validate project.lock.json files
            foreach (var project in projects)
            {
                var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(project.JsonConfigPath);

                if (!File.Exists(lockFilePath))
                {
                    // If the lock file does not exist a restore is needed
                    return true;
                }

                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Read(lockFilePath, referenceContext.Logger);

                var packageSpec = referenceContext.GetOrCreateSpec(project.ProjectName, project.JsonConfigPath);

                if (!lockFile.IsValidForPackageSpec(packageSpec, LockFileFormat.Version))
                {
                    // The project.json file has been changed and the lock file needs to be updated.
                    return true;
                }

                // Verify all libraries are on disk
                var packages = lockFile.Libraries.Where(library => library.Type == LibraryType.Package);

                foreach (var library in packages)
                {
                    var identity = new PackageIdentity(library.Name, library.Version);

                    // Each id/version only needs to be checked once
                    if (packagesChecked.Add(identity))
                    {
                        var found = false;

                        //  Check each package folder. These need to match the order used for restore.
                        foreach (var resolver in pathResolvers)
                        {
                            // Verify the SHA for each package
                            var hashPath = resolver.GetHashPath(library.Name, library.Version);

                            if (File.Exists(hashPath))
                            {
                                found = true;
                                var sha512 = File.ReadAllText(hashPath);

                                if (library.Sha512 != sha512)
                                {
                                    // A package has changed
                                    return true;
                                }

                                // Skip checking the rest of the package folders
                                break;
                            }
                        }

                        if (!found)
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
        public static IReadOnlyList<BuildIntegratedNuGetProject> GetParentProjectsInClosure(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            BuildIntegratedNuGetProject target,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            var parents = new HashSet<BuildIntegratedNuGetProject>();

            var targetProjectJson = Path.GetFullPath(target.JsonConfigPath);

            foreach (var project in projects)
            {
                // do not count the target as a parent
                if (!target.Equals(project))
                {
                    var uniqueName = string.Empty;

                    if (!project.TryGetMetadata(NuGetProjectMetadataKeys.UniqueName, out uniqueName))
                    {
                        uniqueName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                    }

                    BuildIntegratedProjectCacheEntry cacheEntry;

                    if (cache.TryGetValue(uniqueName, out cacheEntry))
                    {
                        // find all projects which have a child reference matching the same project.json path as the target
                        if (cacheEntry.ReferenceClosure.Any(reference =>
                            string.Equals(targetProjectJson, Path.GetFullPath(reference), StringComparison.OrdinalIgnoreCase)))
                        {
                            parents.Add(project);
                        }
                    }
                }
            }

            // sort parents by name to make this more deterministic during restores
            return parents.OrderBy(parent => parent.ProjectName, StringComparer.Ordinal).ToList();
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

        /// <summary>
        /// Find the list of child projects direct or indirect references of target project in
        /// reverse dependency order like the least dependent package first.
        /// </summary>
        public static void GetChildProjectsInClosure(BuildIntegratedNuGetProject target,
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            IList<BuildIntegratedNuGetProject> orderedChilds,
            HashSet<string> uniqueProjectNames,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (orderedChilds == null)
            {
                orderedChilds = new List<BuildIntegratedNuGetProject>();
            }

            if (uniqueProjectNames == null)
            {
                uniqueProjectNames = new HashSet<string>();
            }

            uniqueProjectNames.Add(target.ProjectName);

            if (!orderedChilds.Contains(target))
            {
                var uniqueName = string.Empty;

                if (!target.TryGetMetadata(NuGetProjectMetadataKeys.UniqueName, out uniqueName))
                {
                    uniqueName = target.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                }

                BuildIntegratedProjectCacheEntry cacheEntry;
                if (cache.TryGetValue(uniqueName, out cacheEntry))
                {
                    foreach (var reference in cacheEntry.ReferenceClosure)
                    {
                        var packageSpecPath = Path.GetFullPath(reference);
                        var depProject = projects.FirstOrDefault(
                            proj =>
                                StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(proj.JsonConfigPath),
                                    packageSpecPath));

                        if (depProject != null && !orderedChilds.Contains(depProject) && uniqueProjectNames.Add(depProject.ProjectName))
                        {
                            GetChildProjectsInClosure(depProject, projects, orderedChilds, uniqueProjectNames, cache);
                        }
                    }
                }
                orderedChilds.Add(target);
            }
        }
    }
}
