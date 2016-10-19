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
        public static RestoreArgs GetRestoreArgs(
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            IEnumerable<string> fallbackPackageFolders,
            RestoreCommandProvidersCache providerCache,
            CancellationToken token)
        {
            var args = new RestoreArgs()
            {
            };

            return args;
        }

        public static RestoreArgs GetRestoreArgs(
            IEnumerable<IDependencyGraphProject> projects,
            IEnumerable<string> sources,
            ISettings settings,
            DependencyGraphCacheContext cacheContext,
            SourceCacheContext sourceCacheContext)
        {
            var pathContext = NuGetPathContext.Create(settings);
            var dgSpec = cacheContext.SolutionSpec;

            var providers = new List<IPreLoadedRestoreRequestProvider>();
            var providerCache = new RestoreCommandProvidersCache();

            if (dgSpec.Restore.Count > 0)
            {
                // Only read the dg spec if there is work to do.
                providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgSpec));
            }

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
                FallbackSources = pathContext.FallbackPackageFolders.ToList(),
            };

            return restoreContext;
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> PreviewRestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            DependencyGraphCacheContext context,
            RestoreCommandProviders providers,
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;
            logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            var dgFile = await project.GetDependencyGraphSpecAsync();

            var requests = 

            var request = new RestoreRequest(packageSpec, providers, cacheContext, logger);
            request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;

            // Add the existing lock file if it exists
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(project.JsonConfigPath);
            request.LockFilePath = lockFilePath;
            request.ExistingLockFile = LockFileUtilities.GetLockFile(lockFilePath, logger);

            // Find the full closure of project.json files and referenced projects
            //var projectReferences = await project.GetProjectReferenceClosureAsync(context);
            var dgFile = await project.GetDependencyGraphSpecAsync(context);

            var externalClosure = DependencyGraphSpecRequestProvider.GetExternalClosure(dgFile, project.MSBuildProjectPath).ToList();

            request.ExternalProjects = externalClosure;

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
    }
}