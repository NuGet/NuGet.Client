// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
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
    /// Supporting methods for restoring sets of projects that implement <see cref="IDependencyGraphProject"/>. This
    /// code is used by Visual Studio to execute restores for solutions that have mixtures of UWP project.json,
    /// packages.config, and PackageReference-type projects.
    /// </summary>
    public static class DependencyGraphRestoreUtility
    {
        /// <summary>
        /// Restore a solution and cache the dg spec to context.
        /// </summary>
        public static Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            ISolutionManager solutionManager,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            return RestoreAsync(
                solutionManager,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                userPackagesPath: null,
                settings: settings,
                log: log,
                token: token);
        }

        /// <summary>
        /// Restore a solution and cache the dg spec to context.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            ISolutionManager solutionManager,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            string userPackagesPath,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            // Get full dg spec
            var dgSpec = await GetSolutionRestoreSpec(solutionManager, context);

            // Cache spec
            context.SolutionSpec = dgSpec;
            context.SolutionSpecHash = dgSpec.GetHash();

            // Check if there are actual projects to restore before running.
            if (dgSpec.Restore.Count > 0)
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    // Update cache context
                    cacheContextModifier(sourceCacheContext);

                    var restoreContext = GetRestoreContext(
                        context,
                        providerCache,
                        settings,
                        sourceCacheContext,
                        sources,
                        dgSpec,
                        userPackagesPath);

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, token);

                    RestoreSummary.Log(log, restoreSummaries);

                    return restoreSummaries;
                }
            }

            return new List<RestoreSummary>();
        }

        /// <summary>
        /// Restore a dg spec. This will not update the context cache.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            DependencyGraphSpec dgSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            // Check if there are actual projects to restore before running.
            if (dgSpec.Restore.Count > 0)
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    // Update cache context
                    cacheContextModifier(sourceCacheContext);

                    var restoreContext = GetRestoreContext(
                        context,
                        providerCache,
                        settings,
                        sourceCacheContext,
                        sources,
                        dgSpec,
                        userPackagesPath: null);

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, token);

                    RestoreSummary.Log(log, restoreSummaries);

                    return restoreSummaries;
                }
            }

            return new List<RestoreSummary>();
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static Task<RestoreResultPair> PreviewRestoreAsync(
            ISolutionManager solutionManager,
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            return PreviewRestoreAsync(
                solutionManager,
                project,
                packageSpec,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                userPackagesPath: null,
                settings: settings,
                log: log,
                token: token);
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResultPair> PreviewRestoreAsync(
            ISolutionManager solutionManager,
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            string userPackagesPath,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;

            // Add the new spec to the dg file and fill in the rest.
            var dgFile = await GetSolutionRestoreSpec(solutionManager, context);

            dgFile = dgFile.WithoutRestores()
                .WithReplacedSpec(packageSpec);

            dgFile.AddRestore(project.MSBuildProjectPath);

            using (var sourceCacheContext = new SourceCacheContext())
            {
                // Update cache context
                cacheContextModifier(sourceCacheContext);

                // Settings passed here will be used to populate the restore requests.
                RestoreArgs restoreContext = GetRestoreContext(context, providerCache, settings, sourceCacheContext, sources, dgFile, userPackagesPath);

                var requests = await RestoreRunner.GetRequests(restoreContext);
                var results = await RestoreRunner.RunWithoutCommit(requests, restoreContext);
                return results.Single();
            }
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreProjectAsync(
            ISolutionManager solutionManager,
            BuildIntegratedNuGetProject project,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            // Restore
            var specs = await project.GetPackageSpecsAsync(context);
            var spec = specs.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                || e.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson);

            var result = await PreviewRestoreAsync(
                solutionManager,
                project,
                spec,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                settings,
                log,
                token);

            // Throw before writing if this has been canceled
            token.ThrowIfCancellationRequested();

            // Write out the lock file and msbuild files
            var summary = await RestoreRunner.CommitAsync(result, token);

            RestoreSummary.Log(log, new[] { summary });

            return result.Result;
        }

        public static async Task<bool> IsRestoreRequiredAsync(
            ISolutionManager solutionManager,
            bool forceRestore,
            INuGetPathContext pathContext,
            DependencyGraphCacheContext cacheContext,
            string oldDependencyGraphSpecHash)
        {
            var projects = solutionManager.GetNuGetProjects().OfType<IDependencyGraphProject>().ToArray();

            var solutionDgSpec = await GetSolutionRestoreSpec(solutionManager, cacheContext);

            if (solutionDgSpec.Restore.Count < 1)
            {
                // Nothing to restore
                return false;
            }

            var newDependencyGraphSpecHash = solutionDgSpec.GetHash();
            cacheContext.SolutionSpec = solutionDgSpec;
            cacheContext.SolutionSpecHash = newDependencyGraphSpecHash;

            // Comment by @emgarten from PR -
            // Force is only done during a rebuild, all of the work done here to build the dg file is stored in the cache context and used again later on.
            // The time different should only be the time it takes to create the hash, which @dtivel has perf numbers on.
            if (forceRestore || (oldDependencyGraphSpecHash != newDependencyGraphSpecHash))
            {
                // A new project has been added
                return true;
            }

            // Read package folder locations, initializing them in order of priority
            var packageFolderPaths = new List<string>();
            packageFolderPaths.Add(pathContext.UserPackageFolder);
            packageFolderPaths.AddRange(pathContext.FallbackPackageFolders);
            var pathResolvers = packageFolderPaths.Select(path => new VersionFolderPathResolver(path));

            var packagesChecked = new HashSet<PackageIdentity>();
            if (
                projects.Select(async p => await p.IsRestoreRequired(pathResolvers, packagesChecked, cacheContext))
                    .Any(r => r.Result == true))
            {
                // The project.json file does not match the lock file
                return true;
            }

            if (cacheContext.DeferredPackageSpecs.Where(spec => spec.RestoreMetadata.ProjectJsonPath != null).
                Select(p => IsRestoreRequired(p, pathResolvers, packagesChecked, cacheContext))
                .Any(r => r == true))
            {
                return true;
            }

            return false;
        }

        public static async Task<PackageSpec> GetProjectSpec(IDependencyGraphProject project, DependencyGraphCacheContext context)
        {
            var specs = await project.GetPackageSpecsAsync(context);

            return specs.Where(e => e.RestoreMetadata.ProjectStyle != ProjectStyle.Standalone
                && e.RestoreMetadata.ProjectStyle != ProjectStyle.DotnetCliTool)
                .FirstOrDefault();
        }

        public static async Task<DependencyGraphSpec> GetSolutionRestoreSpec(
            ISolutionManager solutionManager,
            DependencyGraphCacheContext context)
        {
            var dgSpec = new DependencyGraphSpec();

            foreach (var packageSpec in context.DeferredPackageSpecs)
            {
                dgSpec.AddProject(packageSpec);

                if (packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference ||
                    packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson ||
                    packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool ||
                    packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.Standalone)
                {
                    dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
                }
            }

            var projects = solutionManager.GetNuGetProjects().OfType<IDependencyGraphProject>();

            foreach (var project in projects)
            {
                var packageSpecs = await project.GetPackageSpecsAsync(context);

                foreach (var packageSpec in packageSpecs)
                {
                    dgSpec.AddProject(packageSpec);

                    if (packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.Standalone)
                    {
                        dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
                    }
                }
            }
            // Return dg file
            return dgSpec;
        }

        /// <summary>
        /// Create a restore context.
        /// </summary>
        private static RestoreArgs GetRestoreContext(
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            ISettings settings,
            SourceCacheContext sourceCacheContext,
            IEnumerable<SourceRepository> sources,
            DependencyGraphSpec dgFile,
            string userPackagesPath)
        {
            var dgProvider = new DependencyGraphSpecRequestProvider(providerCache, dgFile, settings);

            var restoreContext = new RestoreArgs()
            {
                CacheContext = sourceCacheContext,
                PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>() { dgProvider },
                Log = context.Logger,
                SourceRepositories = sources.ToList(),
                GlobalPackagesFolder = userPackagesPath // Optional, this will load from settings if null
            };

            return restoreContext;
        }

        private static bool IsRestoreRequired(
            PackageSpec packageSpec,
            IEnumerable<VersionFolderPathResolver> pathResolvers,
            ISet<PackageIdentity> packagesChecked,
            DependencyGraphCacheContext context)
        {
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(packageSpec.RestoreMetadata.ProjectJsonPath);

            if (!File.Exists(lockFilePath))
            {
                // If the lock file does not exist a restore is needed
                return true;
            }

            var lockFileFormat = new LockFileFormat();
            LockFile lockFile;
            try
            {
                lockFile = lockFileFormat.Read(lockFilePath, context.Logger);
            }
            catch
            {
                // If the lock file is invalid, then restore.
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

            return false;
        }
    }
}