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
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol;
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
        public static async Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            ISolutionManager solutionManager,
            DependencyGraphSpec dgSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            Guid parentId,
            bool forceRestore,
            bool isRestoreOriginalAction,
            ILogger log,
            CancellationToken token)
        {
            // TODO: This will flow from UI once we enable UI option to trigger reevaluation
            var reevaluateRestoreGraph = false;

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
                        parentId,
                        forceRestore,
                        isRestoreOriginalAction,
                        reevaluateRestoreGraph);

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, token);

                    RestoreSummary.Log(log, restoreSummaries);

                    await PersistDGSpec(dgSpec);

                    return restoreSummaries;
                }
            }

            return new List<RestoreSummary>();
        }

        private static async Task PersistDGSpec(DependencyGraphSpec dgSpec)
        {
            try
            {
                var filePath = Path.Combine(
                        NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp),
                        "nuget-dg",
                        "nugetSpec.dg");

                // create nuget temp folder if not exists
                DirectoryUtility.CreateSharedDirectory(Path.GetDirectoryName(filePath));

                // delete existing dg spec file (if exists) then replace it with new file.
                await FileUtility.ReplaceWithLock(
                    (tempFile) => dgSpec.Save(tempFile), filePath);
            }
            catch (Exception)
            {
                //ignore any failure if it fails to write or replace dg spec file.
            }
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
            Guid parentId,
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
                var restoreContext = GetRestoreContext(
                    context,
                    providerCache,
                    sourceCacheContext,
                    sources,
                    dgFile,
                    parentId,
                    forceRestore: true,
                    isRestoreOriginalAction: false,
                    reevaluateRestoreGraph: true);

                var requests = await RestoreRunner.GetRequests(restoreContext);
                var results = await RestoreRunner.RunWithoutCommit(requests, restoreContext);
                return results.Single();
            }
        }

        /// <summary>
        /// Restore a build integrated project(PackageReference and Project.Json only) and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreProjectAsync(
            ISolutionManager solutionManager,
            BuildIntegratedNuGetProject project,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            IEnumerable<SourceRepository> sources,
            Guid parentId,
            ILogger log,
            CancellationToken token)
        {
            // Restore
            var specs = await project.GetPackageSpecsAsync(context);
            var spec = specs.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                || e.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson); // Do not restore global tools Project Style in VS. 

            var result = await PreviewRestoreAsync(
                solutionManager,
                project,
                spec,
                context,
                providerCache,
                cacheContextModifier,
                sources,
                parentId,
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

            return projectSpec;
        }

        public static async Task<DependencyGraphSpec> GetSolutionRestoreSpec(
            ISolutionManager solutionManager,
            DependencyGraphCacheContext context)
        {
            var dgSpec = new DependencyGraphSpec();

            var projects = (await solutionManager.GetNuGetProjectsAsync()).OfType<IDependencyGraphProject>();

            foreach (var project in projects)
            {
                var packageSpecs = await project.GetPackageSpecsAsync(context);

                foreach (var packageSpec in packageSpecs)
                {
                    dgSpec.AddProject(packageSpec);

                    if (packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool ||
                        packageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.Standalone) // Don't add global tools to restore specs for solutions
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
            Guid parentId,
            bool forceRestore,
            bool isRestoreOriginalAction,
            bool reevaluateRestoreGraph)
        {
            var caching = new CachingSourceProvider(new PackageSourceProvider(context.Settings));
            foreach( var source in sources)
            {
                caching.AddSourceRepository(source);
            }

            var dgProvider = new DependencyGraphSpecRequestProvider(providerCache, dgFile);

            var restoreContext = new RestoreArgs()
            {
                CacheContext = sourceCacheContext,
                PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>() { dgProvider },
                Log = context.Logger,
                AllowNoOp = !forceRestore,
                CachingSourceProvider = caching,
                ParentId = parentId,
                IsRestoreOriginalAction = isRestoreOriginalAction,
                ReevaluateRestoreGraph = reevaluateRestoreGraph
            };

            return restoreContext;
        }
    }
}