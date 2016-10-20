// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
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
        public static async Task<bool> IsRestoreRequiredAsync(
            IEnumerable<IDependencyGraphProject> projects,
            bool forceRestore,
            INuGetPathContext pathContext,
            ExternalProjectReferenceContext referenceContext)
        {
            // Swap caches
            var oldProjectCache = referenceContext.ProjectCache;
            referenceContext.ProjectCache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                projects,
                referenceContext);

            if (forceRestore)
            {
                // The cache has been updated, now skip the check since we are doing a restore anyways.
                return true;
            }

            if (DependencyGraphProjectCacheUtility.CacheHasChanges(oldProjectCache, referenceContext.ProjectCache))
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
            if (projects.Any(p => p.IsRestoreRequired(pathResolvers, packagesChecked, referenceContext)))
            {
                // The project.json file does not match the lock file
                return true;
            }

            return false;
        }

        public static async Task<IReadOnlyList<RestoreSummary>> RestoreAsync(
            IEnumerable<IDependencyGraphProject> projects,
            IEnumerable<string> sources,
            ISettings settings,
            ExternalProjectReferenceContext referenceContext)
        {
            var pathContext = NuGetPathContext.Create(settings);
            var dgSpec = new DependencyGraphSpec();

            foreach (var project in projects)
            {
                var packageSpecs = project.GetPackageSpecsForRestore(referenceContext);

                foreach (var packageSpec in packageSpecs)
                {
                    dgSpec.AddProject(packageSpec);

                    if (packageSpec.RestoreMetadata.OutputType == RestoreOutputType.NETCore ||
                        packageSpec.RestoreMetadata.OutputType == RestoreOutputType.UAP ||
                        packageSpec.RestoreMetadata.OutputType == RestoreOutputType.DotnetCliTool ||
                        packageSpec.RestoreMetadata.OutputType == RestoreOutputType.Standalone)
                    {
                        dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
                    }
                }
            }

            // Check if there are actual projects to restore before running.
            if (dgSpec.Restore.Count > 0)
            {
                using (var cacheContext = new SourceCacheContext())
                {
                    var providers = new List<IPreLoadedRestoreRequestProvider>();
                    var providerCache = new RestoreCommandProvidersCache();
                    providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgSpec));

                    var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(settings));

                    var restoreContext = new RestoreArgs
                    {
                        CacheContext = cacheContext,
                        LockFileVersion = LockFileFormat.Version,
                        ConfigFile = null,
                        DisableParallel = false,
                        GlobalPackagesFolder = pathContext.UserPackageFolder,
                        Log = referenceContext.Logger,
                        MachineWideSettings = new XPlatMachineWideSetting(),
                        PreLoadedRequestProviders = providers,
                        CachingSourceProvider = sourceProvider,
                        Sources = sources.ToList()
                    };

                    var restoreSummaries = await RestoreRunner.Run(restoreContext);
                    RestoreSummary.Log(referenceContext.Logger, restoreSummaries);

                    return restoreSummaries;
                }
            }

            return new List<RestoreSummary>();
        }
    }
}
