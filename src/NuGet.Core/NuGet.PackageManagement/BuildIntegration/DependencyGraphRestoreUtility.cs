// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
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
        public static async Task RestoreAsync(
           IEnumerable<IDependencyGraphProject> projects,
           IEnumerable<string> sources,
           ISettings settings,
           DependencyGraphCacheContext cacheContext)
        {
            var pathContext = NuGetPathContext.Create(settings);
            var dgSpec = cacheContext.SolutionSpec;

            // Check if there are actual projects to restore before running.
            if (dgSpec.Restore.Count > 0)
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    var providers = new List<IPreLoadedRestoreRequestProvider>();
                    var providerCache = new RestoreCommandProvidersCache();
                    providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgSpec));

                    var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(settings));

                    var restoreContext = new RestoreArgs
                    {
                        CacheContext = sourceCacheContext,
                        LockFileVersion = LockFileFormat.Version,
                        ConfigFile = null,
                        DisableParallel = false,
                        GlobalPackagesFolder = pathContext.UserPackageFolder,
                        Log = cacheContext.Logger,
                        MachineWideSettings = new XPlatMachineWideSetting(),
                        PreLoadedRequestProviders = providers,
                        CachingSourceProvider = sourceProvider,
                        Sources = sources.ToList(),
                        FallbackSources = pathContext.FallbackPackageFolders.ToList()
                    };

                    var restoreSummaries = await RestoreRunner.Run(restoreContext);

                    RestoreSummary.Log(cacheContext.Logger, restoreSummaries);
                }
            }
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResultPair> PreviewRestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            ISettings settings,
            SourceCacheContext sourceCacheContext,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;

            // Add the new spec to the dg file and fill in the rest.
            var dgFile = DependencyGraphSpec.WithReplacedSpec(await project.GetDependencyGraphSpecAsync(context), packageSpec);
            dgFile.AddRestore(project.MSBuildProjectPath);

            // Settings passed here will be used to populate the restore requests.
            var dgProvider = new DependencyGraphSpecRequestProvider(providerCache, dgFile, settings);

            var restoreContext = new RestoreArgs()
            {
                CacheContext = sourceCacheContext,
                PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>() { dgProvider },
                Log = context.Logger
            };

            var requests = await RestoreRunner.GetRequests(restoreContext);
            var results = await RestoreRunner.RunWithoutCommit(requests, restoreContext);

            return results.Single();
        }

        public static async Task<bool> IsRestoreRequiredAsync(
            IEnumerable<IDependencyGraphProject> projects,
            bool forceRestore,
            INuGetPathContext pathContext,
            DependencyGraphCacheContext cacheContext,
            int oldDependencyGraphSpecHash)
        {
            if (forceRestore)
            {
                // The cache has been updated, now skip the check since we are doing a restore anyways.
                return true;
            }

            var solutionDgSpec = await DependencyGraphProjectCacheUtility.GetSolutionRestoreSpec(projects, cacheContext);
            var newDependencyGraphSpecHash = solutionDgSpec.GetHashCode();
            cacheContext.SolutionSpec = solutionDgSpec;
            cacheContext.SolutionSpecHash = newDependencyGraphSpecHash;

            if (oldDependencyGraphSpecHash != newDependencyGraphSpecHash)
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

            return false;
        }

        /// <summary>
        /// Find the list of child projects direct or indirect references of target project in
        /// reverse dependency order like the least dependent package first.
        /// </summary>
        public static IEnumerable<BuildIntegratedNuGetProject> GetChildProjectsInClosure(
            BuildIntegratedNuGetProject target,
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            DependencyGraphSpec cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var orderedChildren = new List<BuildIntegratedNuGetProject>();

            var listOfPackageSpecs = cache.GetClosure(target.MSBuildProjectPath);

            foreach (var spec in listOfPackageSpecs)
            {
                var proj = projects.FirstOrDefault(p => p.MSBuildProjectPath == spec.RestoreMetadata.ProjectUniqueName);
                orderedChildren.Add(proj);
            }

            return orderedChildren;
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreProjectAsync(
            BuildIntegratedNuGetProject project,
            DependencyGraphCacheContext context,
            RestoreCommandProvidersCache providerCache,
            Action<SourceCacheContext> cacheContextModifier,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContextModifier(cacheContext);

                // Restore
                var result = await PreviewRestoreAsync(
                    project,
                    await project.GetPackageSpecAsync(context),
                    context,
                    providerCache,
                    settings,
                    cacheContext,
                    token);

                // Throw before writing if this has been canceled
                token.ThrowIfCancellationRequested();

                // Write out the lock file and msbuild files
                var summary = await RestoreRunner.Commit(result);

                return result.Result;
            }
        }
    }
}