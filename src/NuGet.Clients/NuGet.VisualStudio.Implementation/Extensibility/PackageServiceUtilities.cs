// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal static class PackageServiceUtilities
    {
        /// <summary>
        /// Checks whether the specified package and version are in the list.
        /// If the nugetVersion is null, then this method only checks whether given package id is in list.
        /// </summary>
        /// <param name="installedPackageReferences">installed package references</param>
        /// <param name="packageId">packageId to check, can't be null or empty.</param>
        /// <param name="nugetVersion">nuGetVersion to check, can be null.</param>
        /// <returns>Whether the package is in the list.</returns>
        /// <exception cref="ArgumentNullException"> if <paramref name="installedPackageReferences"/> is null</exception>
        /// <exception cref="ArgumentException"> if <paramref name="packageId"/> is null or empty</exception>
        internal static bool IsPackageInList(IEnumerable<PackageReference> installedPackageReferences, string packageId, NuGetVersion nugetVersion)
        {
            if (installedPackageReferences == null)
            {
                throw new ArgumentNullException(nameof(installedPackageReferences));
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageId));
            }

            return installedPackageReferences.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId) &&
                (nugetVersion != null ?
                    VersionComparer.VersionRelease.Equals(p.PackageIdentity.Version, nugetVersion) :
                    true));
        }

        internal static NuGetPackageManager CreatePackageManager(ISourceRepositoryProvider repoProvider, ISettings settings, IVsSolutionManager solutionManager, IDeleteOnRestartManager deleteOnRestartManager, IRestoreProgressReporter progressReporter)
        {
            return new NuGetPackageManager(
                repoProvider,
                settings,
                solutionManager,
                deleteOnRestartManager,
                progressReporter);
        }

        /// <summary>
        /// Core install method. All installs from the VS API and template wizard end up here.
        /// This does not check for already installed packages
        /// </summary>
        internal static async Task InstallInternalCoreAsync(
            NuGetPackageManager packageManager,
            GatherCache gatherCache,
            NuGetProject nuGetProject,
            PackageIdentity package,
            IEnumerable<SourceRepository> sources,
            VSAPIProjectContext projectContext,
            bool includePrerelease,
            bool ignoreDependencies,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            var depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;

            using (var sourceCacheContext = new SourceCacheContext())
            {
                var resolution = new ResolutionContext(
                    depBehavior,
                    includePrerelease,
                    includeUnlisted: false,
                    versionConstraints: VersionConstraints.None,
                    gatherCache: gatherCache,
                    sourceCacheContext: sourceCacheContext);

                // install the package
                if (package.Version == null)
                {
                    await packageManager.InstallPackageAsync(nuGetProject, package.Id, resolution, projectContext, sources, Enumerable.Empty<SourceRepository>(), token);
                }
                else
                {
                    await packageManager.InstallPackageAsync(nuGetProject, package, resolution, projectContext, sources, Enumerable.Empty<SourceRepository>(), token);
                }
            }
        }

        /// <summary>
        /// Internal install method. All installs from the VS API and template wizard end up here.
        /// </summary>
        internal static async Task InstallInternalAsync(
            Func<IVsSolutionManager, Task<NuGetProject>> getProjectAsync,
            List<PackageIdentity> packages,
            ISourceRepositoryProvider repoProvider,
            IVsSolutionManager solutionManager,
            ISettings settings,
            IDeleteOnRestartManager deleteOnRestartManager,
            VSAPIProjectContext projectContext,
            bool includePrerelease,
            bool ignoreDependencies,
            CancellationToken token)
        {
            // Go off the UI thread. This may be called from the UI thread. Only switch to the UI thread where necessary
            // This method installs multiple packages and can likely take more than a few secs
            // So, go off the UI thread explicitly to improve responsiveness
            await TaskScheduler.Default;

            var sources = repoProvider.GetRepositories().ToList();

            // store expanded node state
            var expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync();

            try
            {
                var depBehavior = ignoreDependencies ? DependencyBehavior.Ignore : DependencyBehavior.Lowest;
                // TODO NK - Need to actually provide the progress reported.
                var packageManager = CreatePackageManager(repoProvider, settings, solutionManager, deleteOnRestartManager, null);

                // Get the project
                NuGetProject nuGetProject = await getProjectAsync(solutionManager);

                var packageManagementFormat = new PackageManagementFormat(settings);
                // 1 means PackageReference
                var preferPackageReference = packageManagementFormat.SelectedPackageManagementFormat == 1;

                // Check if default package format is set to `PackageReference` and project has no
                // package installed yet then upgrade it to `PackageReference` based project.
                if (preferPackageReference &&
                   (nuGetProject is MSBuildNuGetProject) &&
                   !(await nuGetProject.GetInstalledPackagesAsync(token)).Any() &&
                   await NuGetProjectUpgradeUtility.IsNuGetProjectUpgradeableAsync(nuGetProject, needsAPackagesConfig: false))
                {
                    nuGetProject = await solutionManager.UpgradeProjectToPackageReferenceAsync(nuGetProject);
                }

                // install the package
                foreach (var package in packages)
                {
                    var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);
                    if (IsPackageInList(installedPackages, package.Id, package.Version))
                    {
                        continue;
                    }

                    // Perform the install
                    await InstallInternalCoreAsync(
                        packageManager,
                        new GatherCache(),
                        nuGetProject,
                        package,
                        sources,
                        projectContext,
                        includePrerelease,
                        ignoreDependencies,
                        token);
                }
            }
            finally
            {
                // collapse nodes
                await VsHierarchyUtility.CollapseAllNodesAsync(expandedNodes);
            }
        }

    }
}
