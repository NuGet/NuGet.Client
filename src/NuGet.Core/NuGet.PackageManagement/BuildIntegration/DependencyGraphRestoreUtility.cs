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
using NuGet.Shared;

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
            bool forceRestore,
            ILogger log,
            CancellationToken token)
        {
            return RestoreAsync(
                solutionManager,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                userPackagesPath: null, // TODO NK - Why is this null? What happens when it's null
                log: log,
                forceRestore: forceRestore,
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
            string userPackagesPath, // Is this ever not null?
            bool forceRestore,
            ILogger log,
            CancellationToken token)
        {
            // Get full dg spec
            var dgSpec = await GetSolutionRestoreSpec(solutionManager, context);

            // Cache spec TODO NK - Why do we cache the spec?
            context.SolutionSpec = dgSpec;

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
                        sourceCacheContext,
                        sources,
                        dgSpec,
                        userPackagesPath,
                        forceRestore);

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, token);

                    RestoreSummary.Log(log, restoreSummaries);

                    return restoreSummaries;
                }
            }

            return new List<RestoreSummary>();
        }

        /// <summary>
        /// Restore a dg spec. This will not update the context cache. // TODO NK - Why? :D 
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            DependencyGraphSpec dgSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            bool forceRestore,
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
                        sourceCacheContext,
                        sources,
                        dgSpec,
                        userPackagesPath: null,
                        forceRestore: forceRestore);

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
                RestoreArgs restoreContext = GetRestoreContext(context, providerCache, sourceCacheContext, sources, dgFile, userPackagesPath, false); // TODO NK - Do we want to force in preview? 

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
            ILogger log,
            CancellationToken token)
        {
            // Restore
            var specs = await project.GetPackageSpecsAsync(context);
            var spec = specs.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                || e.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson);
            // TODO NK - Might be a better to move this to the root where we call project.GetPackageSpecsAsync
            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(context.Settings);
            var fallbackFolders = SettingsUtility.GetFallbackPackageFolders(context.Settings);
            spec.RestoreMetadata.FallbackFolders = spec.RestoreMetadata.FallbackFolders ?? fallbackFolders.AsList();
            spec.RestoreMetadata.PackagesPath = spec.RestoreMetadata.PackagesPath ?? globalPackagesFolder;

            var result = await PreviewRestoreAsync(
                solutionManager,
                project,
                spec,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                log,
                token);

            // Throw before writing if this has been canceled
            token.ThrowIfCancellationRequested();

            // Write out the lock file and msbuild files
            var summary = await RestoreRunner.CommitAsync(result, token);

            RestoreSummary.Log(log, new[] { summary });

            return result.Result;
        }

        public static bool IsRestoreRequired(
            DependencyGraphSpec solutionDgSpec)
        {
            if (solutionDgSpec.Restore.Count < 1)
            {
                // Nothing to restore
                return false;
            }

            // NO Op will be checked in the restore command 
            return true;
        }

        public static async Task<PackageSpec> GetProjectSpec(IDependencyGraphProject project, DependencyGraphCacheContext context)
        {
            var specs = await project.GetPackageSpecsAsync(context);

            var projectSpec =  specs.Where(e => e.RestoreMetadata.ProjectStyle != ProjectStyle.Standalone
                && e.RestoreMetadata.ProjectStyle != ProjectStyle.DotnetCliTool)
                .FirstOrDefault();

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(context.Settings);
            var fallbackFolders = SettingsUtility.GetFallbackPackageFolders(context.Settings);
            projectSpec.RestoreMetadata.FallbackFolders = projectSpec.RestoreMetadata.FallbackFolders ?? fallbackFolders.AsList();
            projectSpec.RestoreMetadata.PackagesPath = projectSpec.RestoreMetadata.PackagesPath ?? globalPackagesFolder;

            return projectSpec;
        }

        public static async Task<DependencyGraphSpec> GetSolutionRestoreSpec(
            ISolutionManager solutionManager,
            DependencyGraphCacheContext context)
        {
            var dgSpec = new DependencyGraphSpec();
            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(context.Settings);
            var fallbackFolders = SettingsUtility.GetFallbackPackageFolders(context.Settings);

            foreach (var packageSpec in context.DeferredPackageSpecs)
            {
                //TODO NK - Does this really make sense? Anything unforeseen here? 
                packageSpec.RestoreMetadata.FallbackFolders = fallbackFolders.AsList();
                packageSpec.RestoreMetadata.PackagesPath = globalPackagesFolder;

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
                    packageSpec.RestoreMetadata.FallbackFolders = fallbackFolders.AsList();
                    packageSpec.RestoreMetadata.PackagesPath = globalPackagesFolder;

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
            SourceCacheContext sourceCacheContext,
            IEnumerable<SourceRepository> sources,
            DependencyGraphSpec dgFile,
            string userPackagesPath,
            bool forceRestore)
        {
            var dgProvider = new DependencyGraphSpecRequestProvider(providerCache, dgFile);

            var restoreContext = new RestoreArgs()
            {
                CacheContext = sourceCacheContext,
                PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>() { dgProvider },
                Log = context.Logger,
                SourceRepositories = sources.ToList(),
                GlobalPackagesFolder = userPackagesPath, // Optional, this will load from settings if null
                AllowNoOp = !forceRestore
            };

            return restoreContext;
        }
    }
}