// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// NuGetPackageManager orchestrates a nuget package operation such as an install or uninstall
    /// It is to be called by various NuGet Clients including the custom third-party ones
    /// </summary>
    public class NuGetPackageManager
    {
        private ISourceRepositoryProvider SourceRepositoryProvider { get; set; }

        private ISolutionManager SolutionManager { get; }

        private ISettings Settings { get; }

        public FolderNuGetProject PackagesFolderNuGetProject { get; set; }

        public SourceRepository PackagesFolderSourceRepository { get; set; }

        /// <summary>
        /// To construct a NuGetPackageManager that does not need a SolutionManager like NuGet.exe
        /// </summary>
        public NuGetPackageManager(ISourceRepositoryProvider sourceRepositoryProvider, string packagesFolderPath)
        {
            InitializeMandatory(sourceRepositoryProvider);
            if (packagesFolderPath == null)
            {
                throw new ArgumentNullException(nameof(packagesFolderPath));
            }

            InitializePackagesFolderInfo(packagesFolderPath);
        }

        /// <summary>
        /// To construct a NuGetPackageManager with a mandatory SolutionManager lke VS
        /// </summary>
        public NuGetPackageManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
        {
            InitializeMandatory(sourceRepositoryProvider);
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            Settings = settings;
            SolutionManager = solutionManager;

            InitializePackagesFolderInfo(PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings));
        }

        private void InitializeMandatory(ISourceRepositoryProvider sourceRepositoryProvider)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositoryProvider));
            }

            SourceRepositoryProvider = sourceRepositoryProvider;
        }

        private void InitializePackagesFolderInfo(string packagesFolderPath)
        {
            PackagesFolderNuGetProject = new FolderNuGetProject(packagesFolderPath);
            PackagesFolderSourceRepository = SourceRepositoryProvider.CreateRepository(new PackageSource(packagesFolderPath));
        }

        /// <summary>
        /// Installs the latest version of the given <paramref name="packageId" /> to NuGetProject
        /// <paramref name="nuGetProject" /> <paramref name="resolutionContext" /> and
        /// <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(NuGetProject nuGetProject, string packageId, ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext, SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            return InstallPackageAsync(nuGetProject, packageId, resolutionContext, nuGetProjectContext,
                new List<SourceRepository> { primarySourceRepository }, secondarySources, token);
        }

        /// <summary>
        /// Installs the latest version of the given
        /// <paramref name="packageId" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject nuGetProject, string packageId, ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext, IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            // Step-1 : Get latest version for packageId
            var latestVersion = await GetLatestVersionAsync(packageId, resolutionContext, primarySources, token);

            if (latestVersion == null)
            {
                throw new InvalidOperationException(string.Format(Strings.NoLatestVersionFound, packageId));
            }

            // Step-2 : Call InstallPackageAsync(project, packageIdentity)
            await InstallPackageAsync(nuGetProject, new PackageIdentity(packageId, latestVersion), resolutionContext,
                nuGetProjectContext, primarySources, secondarySources, token);
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity, ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext, SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            return InstallPackageAsync(nuGetProject, packageIdentity, resolutionContext, nuGetProjectContext,
                new List<SourceRepository> { primarySourceRepository }, secondarySources, token);
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task InstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity, ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext, IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            // Step-1 : Call PreviewInstallPackageAsync to get all the nuGetProjectActions
            var nuGetProjectActions = await PreviewInstallPackageAsync(nuGetProject, packageIdentity, resolutionContext,
                nuGetProjectContext, primarySources, secondarySources, token);

            SetDirectInstall(packageIdentity, nuGetProjectContext);

            // Step-2 : Execute all the nuGetProjectActions
            await ExecuteNuGetProjectActionsAsync(nuGetProject, nuGetProjectActions, nuGetProjectContext, token);

            ClearDirectInstall(nuGetProjectContext);
        }

        public async Task UninstallPackageAsync(NuGetProject nuGetProject, string packageId, UninstallationContext uninstallationContext,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Step-1 : Call PreviewUninstallPackagesAsync to get all the nuGetProjectActions
            var nuGetProjectActions = await PreviewUninstallPackageAsync(nuGetProject, packageId, uninstallationContext, nuGetProjectContext, token);

            // Step-2 : Execute all the nuGetProjectActions
            await ExecuteNuGetProjectActionsAsync(nuGetProject, nuGetProjectActions, nuGetProjectContext, token);
        }

        /// <summary>
        /// Gives the preview as a list of NuGetProjectActions that will be performed to install
        /// <paramref name="packageId" /> into <paramref name="nuGetProject" /> <paramref name="resolutionContext" />
        /// and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(NuGetProject nuGetProject, string packageId,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository, IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (resolutionContext == null)
            {
                throw new ArgumentNullException(nameof(resolutionContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }
            // Step-1 : Get latest version for packageId
            var latestVersion = await GetLatestVersionAsync(packageId, resolutionContext, primarySourceRepository, token);

            if (latestVersion == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownPackage, packageId));
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var installedPackageReference = projectInstalledPackageReferences.Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, packageId)).FirstOrDefault();
            if (installedPackageReference != null
                && installedPackageReference.PackageIdentity.Version > latestVersion)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.NewerVersionAlreadyReferenced, packageId));
            }

            // Step-2 : Call InstallPackage(project, packageIdentity)
            return await PreviewInstallPackageAsync(nuGetProject, new PackageIdentity(packageId, latestVersion), resolutionContext,
                nuGetProjectContext, primarySourceRepository, secondarySources, token);
        }

        public async Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(IEnumerable<string> packageIdsToInstall, NuGetProject nuGetProject,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository, IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            if (packageIdsToInstall == null)
            {
                throw new ArgumentNullException(nameof(packageIdsToInstall));
            }

            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (resolutionContext == null)
            {
                throw new ArgumentNullException(nameof(resolutionContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (packageIdsToInstall.Any(p => string.IsNullOrEmpty(p)))
            {
                throw new ArgumentException(nameof(packageIdsToInstall));
            }

            if (primarySourceRepository == null)
            {
                throw new ArgumentNullException(nameof(primarySourceRepository));
            }
            var primarySources = new List<SourceRepository> { primarySourceRepository };

            if (secondarySources == null)
            {
                secondarySources = SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            // Note: resolver needs all the installed packages as targets too. And, metadata should be gathered for the installed packages as well
            var packageTargetIdsForResolver = new HashSet<string>(oldListOfInstalledPackages.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var packageIdToInstall in packageIdsToInstall)
            {
                packageTargetIdsForResolver.Add(packageIdToInstall);
            }

            var nuGetProjectActions = new List<NuGetProjectAction>();
            if (!packageTargetIdsForResolver.Any())
            {
                return nuGetProjectActions;
            }
            // TODO: these sources should be ordered
            // TODO: search in only the active source but allow dependencies to come from other sources?

            var effectiveSources = GetEffectiveSources(primarySources, secondarySources);

            try
            {
                // If any targets are prerelease we should gather with prerelease on and filter afterwards
                var includePrereleaseInGather = resolutionContext.IncludePrerelease || (projectInstalledPackageReferences.Any(p => (p.PackageIdentity.HasVersion && p.PackageIdentity.Version.IsPrerelease)));
                var contextForGather = new ResolutionContext(resolutionContext.DependencyBehavior, includePrereleaseInGather, resolutionContext.IncludeUnlisted);

                // Step-1 : Get metadata resources using gatherer
                var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToGatherDependencyInfoForMultiplePackages, projectName, targetFramework);
                var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherPackageDependencyInfo(
                    packageIdsToInstall,
                    Enumerable.Empty<PackageIdentity>(),
                    oldListOfInstalledPackages,
                    targetFramework,
                    primarySources,
                    effectiveSources,
                    PackagesFolderSourceRepository,
                    token);

                if (!availablePackageDependencyInfoWithSourceSet.Any())
                {
                    throw new InvalidOperationException(Strings.UnableToGatherDependencyInfoForMultiplePackages);
                }

                // Prune the results down to only what we would allow to be installed
                IEnumerable<SourcePackageDependencyInfo> prunedAvailablePackages = availablePackageDependencyInfoWithSourceSet;

                if (!resolutionContext.IncludePrerelease)
                {
                    prunedAvailablePackages = PrunePackageTree.PrunePreleaseForStableTargets(prunedAvailablePackages,
                        projectInstalledPackageReferences.Select(p => p.PackageIdentity));
                }

                // Verify that the target is allowed by packages.config
                ResolverGather.ThrowIfVersionIsDisallowedByPackagesConfig(packageIdsToInstall, projectInstalledPackageReferences, prunedAvailablePackages);

                // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                // Step-2 : Call PackageResolver.Resolve to get new list of installed packages
                var packageResolver = new PackageResolver();
                var packageResolverContext = new PackageResolverContext(resolutionContext.DependencyBehavior,
                    packageIdsToInstall,
                    packageTargetIdsForResolver,
                    projectInstalledPackageReferences,
                    Enumerable.Empty<PackageIdentity>(),
                    prunedAvailablePackages);

                nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToResolveDependenciesForMultiplePackages);
                var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);
                if (newListOfInstalledPackages == null)
                {
                    throw new InvalidOperationException(Strings.UnableToResolveDependencyInfoForMultiplePackages);
                }

                nuGetProjectActions = GetProjectActionsForUpdate(newListOfInstalledPackages, oldListOfInstalledPackages, prunedAvailablePackages, nuGetProjectContext);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (AggregateException aggregateEx)
            {
                throw new InvalidOperationException(aggregateEx.Message, aggregateEx);
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(ex.Message))
                {
                    throw new InvalidOperationException(Strings.PackagesCouldNotBeInstalled, ex);
                }
                throw new InvalidOperationException(ex.Message, ex);
            }
            return nuGetProjectActions;
        }

        public async Task<IEnumerable<NuGetProjectAction>> PreviewReinstallPackagesAsync(IEnumerable<PackageIdentity> packagesToInstall, NuGetProject nuGetProject,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository, IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            if (packagesToInstall == null)
            {
                throw new ArgumentNullException(nameof(packagesToInstall));
            }

            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (resolutionContext == null)
            {
                throw new ArgumentNullException(nameof(resolutionContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (packagesToInstall.Any(p => p.Version == null))
            {
                throw new ArgumentException(nameof(packagesToInstall));
            }

            if (primarySourceRepository == null)
            {
                throw new ArgumentNullException(nameof(primarySourceRepository));
            }
            var primarySources = new List<SourceRepository> { primarySourceRepository };

            if (secondarySources == null)
            {
                secondarySources = SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            // Note: resolver needs all the installed packages as targets too. And, metadata should be gathered for the installed packages as well
            var packageTargetsForResolver = new HashSet<PackageIdentity>(oldListOfInstalledPackages, PackageIdentity.Comparer);
            foreach (var packageToInstall in packagesToInstall)
            {
                packageTargetsForResolver.Add(packageToInstall);
            }

            var nuGetProjectActions = new List<NuGetProjectAction>();
            // TODO: these sources should be ordered
            // TODO: search in only the active source but allow dependencies to come from other sources?

            var effectiveSources = GetEffectiveSources(primarySources, secondarySources);

            try
            {
                // If any targets are prerelease we should gather with prerelease on and filter afterwards
                var includePrereleaseInGather = resolutionContext.IncludePrerelease || (packageTargetsForResolver.Any(p => (p.HasVersion && p.Version.IsPrerelease)));
                var contextForGather = new ResolutionContext(resolutionContext.DependencyBehavior, includePrereleaseInGather, resolutionContext.IncludeUnlisted);

                // Step-1 : Get metadata resources using gatherer
                var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToGatherDependencyInfoForMultiplePackages, projectName, targetFramework);
                var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherPackageDependencyInfo(
                    packagesToInstall,
                    oldListOfInstalledPackages,
                    targetFramework,
                    primarySources,
                    effectiveSources,
                    PackagesFolderSourceRepository,
                    token);

                if (!availablePackageDependencyInfoWithSourceSet.Any())
                {
                    throw new InvalidOperationException(Strings.UnableToGatherDependencyInfoForMultiplePackages);
                }

                // Prune the results down to only what we would allow to be installed
                IEnumerable<SourcePackageDependencyInfo> prunedAvailablePackages = availablePackageDependencyInfoWithSourceSet;

                // Keep only the target package we are trying to install for that Id
                foreach (var packageIdentity in packagesToInstall)
                {
                    prunedAvailablePackages = PrunePackageTree.RemoveAllVersionsForIdExcept(prunedAvailablePackages, packageIdentity);
                }

                if (!resolutionContext.IncludePrerelease)
                {
                    prunedAvailablePackages = PrunePackageTree.PrunePreleaseForStableTargets(prunedAvailablePackages, packageTargetsForResolver);
                }

                // Verify that the target is allowed by packages.config
                ResolverGather.ThrowIfVersionIsDisallowedByPackagesConfig(packagesToInstall.Select(p => p.Id), projectInstalledPackageReferences, prunedAvailablePackages);

                // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                // Step-2 : Call IPackageResolver.Resolve to get new list of installed packages                
                nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToResolveDependenciesForMultiplePackages);

                var packageResolver = new PackageResolver();

                var packageResolverContext = new PackageResolverContext(resolutionContext.DependencyBehavior,
                    packagesToInstall.Select(p => p.Id),
                    packageTargetsForResolver.Select(package => package.Id),
                    projectInstalledPackageReferences,
                    packageTargetsForResolver,
                    prunedAvailablePackages);

                var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);
                if (newListOfInstalledPackages == null)
                {
                    throw new InvalidOperationException(Strings.UnableToResolveDependencyInfoForMultiplePackages);
                }

                var packagesInDependencyOrder = (await GetInstalledPackagesInDependencyOrder(nuGetProject,
                    token)).Reverse();

                foreach (var package in packagesInDependencyOrder)
                {
                    nuGetProjectActions.Add(NuGetProjectAction.CreateUninstallProjectAction(package));
                }

                var comparer = PackageIdentity.Comparer;
                foreach (var newPackageToInstall in newListOfInstalledPackages)
                {
                    // find the package match based on identity
                    var sourceDepInfo = availablePackageDependencyInfoWithSourceSet.Where(p => comparer.Equals(p, newPackageToInstall)).SingleOrDefault();

                    if (sourceDepInfo == null)
                    {
                        // this really should never happen
                        throw new InvalidOperationException(string.Format(Strings.PackageNotFound, newPackageToInstall));
                    }

                    nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(newPackageToInstall, sourceDepInfo.Source));
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (AggregateException aggregateEx)
            {
                throw new InvalidOperationException(aggregateEx.Message, aggregateEx);
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(ex.Message))
                {
                    throw new InvalidOperationException(Strings.PackagesCouldNotBeInstalled, ex);
                }
                throw new InvalidOperationException(ex.Message, ex);
            }
            return nuGetProjectActions;
        }

        /// <summary>
        /// Returns all installed packages in order of dependency. Packages with no dependencies come first.
        /// </summary>
        /// <remarks>Packages with unresolved dependencies are NOT returned since they are not valid.</remarks>
        public async Task<IEnumerable<PackageIdentity>> GetInstalledPackagesInDependencyOrder(NuGetProject nuGetProject,
            CancellationToken token)
        {
            var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);
            var installedPackageIdentities = installedPackages.Select(pr => pr.PackageIdentity);
            var dependencyInfoFromPackagesFolder = await GetDependencyInfoFromPackagesFolder(installedPackageIdentities,
                targetFramework);

            // dependencyInfoFromPackagesFolder can be null when NuGetProtocolException is thrown
            var resolverPackages = dependencyInfoFromPackagesFolder?.Select(package =>
                    new ResolverPackage(package.Id, package.Version, package.Dependencies, true, false));

            // Use the resolver sort to find the order. Packages with no dependencies 
            // come first, then each package that has satisfied dependencies. 
            // Packages with missing dependencies will not be returned.
            if (resolverPackages != null)
            {
                return ResolverUtility.TopologicalSort(resolverPackages);
            }

            return Enumerable.Empty<PackageIdentity>();
        }

        // TODO: Convert this to a generic GetProjectActions and use it from Install methods too
        private static List<NuGetProjectAction> GetProjectActionsForUpdate(IEnumerable<PackageIdentity> newListOfInstalledPackages,
            IEnumerable<PackageIdentity> oldListOfInstalledPackages,
            IEnumerable<SourcePackageDependencyInfo> availablePackageDependencyInfoWithSourceSet,
            INuGetProjectContext nuGetProjectContext)
        {
            // Step-3 : Get the list of nuGetProjectActions to perform, install/uninstall on the nugetproject
            // based on newPackages obtained in Step-2 and project.GetInstalledPackages
            var nuGetProjectActions = new List<NuGetProjectAction>();
            nuGetProjectContext.Log(MessageLevel.Info, Strings.ResolvingActionsToInstallOrUpdateMultiplePackages);
            var newPackagesToUninstall = oldListOfInstalledPackages
                .Where(op => newListOfInstalledPackages
                    .Where(np => op.Id.Equals(np.Id, StringComparison.OrdinalIgnoreCase) && !op.Version.Equals(np.Version)).Any());
            var newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));

            foreach (var newPackageToUninstall in newPackagesToUninstall)
            {
                nuGetProjectActions.Add(NuGetProjectAction.CreateUninstallProjectAction(newPackageToUninstall));
            }

            var comparer = PackageIdentity.Comparer;

            foreach (var newPackageToInstall in newPackagesToInstall)
            {
                // find the package match based on identity
                var sourceDepInfo = availablePackageDependencyInfoWithSourceSet.Where(p => comparer.Equals(p, newPackageToInstall)).SingleOrDefault();

                if (sourceDepInfo == null)
                {
                    // this really should never happen
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentUICulture, Strings.PackageNotFound, newPackageToInstall));
                }

                nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(newPackageToInstall, sourceDepInfo.Source));
            }

            return nuGetProjectActions;
        }

        /// <summary>
        /// Gives the preview as a list of NuGetProjectActions that will be performed to install
        /// <paramref name="packageIdentity" /> into <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository, IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, primarySourceRepository);
                return Task.FromResult<IEnumerable<NuGetProjectAction>>(new[] { action });
            }

            var primarySources = new List<SourceRepository> { primarySourceRepository };
            return PreviewInstallPackageAsync(nuGetProject, packageIdentity, resolutionContext,
                nuGetProjectContext, primarySources, secondarySources, token);
        }

        public async Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources, IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (resolutionContext == null)
            {
                throw new ArgumentNullException(nameof(resolutionContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (primarySources == null)
            {
                throw new ArgumentNullException(nameof(primarySources));
            }

            if (secondarySources == null)
            {
                secondarySources = SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);
            }

            if (!primarySources.Any())
            {
                throw new ArgumentException(nameof(primarySources));
            }

            if (packageIdentity.Version == null)
            {
                throw new ArgumentNullException("packageIdentity.Version");
            }

            // TODO: BUGBUG: HACK: Multiple primary repositories is mainly intended for nuget.exe at the moment
            // The following special case for ProjectK is not correct, if they used nuget.exe
            // and multiple repositories in the -Source switch
            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, primarySources.First());
                return new[] { action };
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);
            if (oldListOfInstalledPackages.Any(p => p.Equals(packageIdentity)))
            {
                string projectName;
                nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.Name, out projectName);
                var alreadyInstalledMessage = string.Format(ProjectManagement.Strings.PackageAlreadyExistsInProject, packageIdentity, projectName ?? string.Empty);
                throw new InvalidOperationException(alreadyInstalledMessage, new PackageAlreadyInstalledException(alreadyInstalledMessage));
            }

            var nuGetProjectActions = new List<NuGetProjectAction>();
            // TODO: these sources should be ordered
            // TODO: search in only the active source but allow dependencies to come from other sources?

            var effectiveSources = GetEffectiveSources(primarySources, secondarySources);

            if (resolutionContext.DependencyBehavior != DependencyBehavior.Ignore)
            {
                try
                {
                    var downgradeAllowed = false;
                    var packageTargetsForResolver = new HashSet<PackageIdentity>(oldListOfInstalledPackages, PackageIdentity.Comparer);
                    // Note: resolver needs all the installed packages as targets too. And, metadata should be gathered for the installed packages as well
                    var installedPackageWithSameId = packageTargetsForResolver.Where(p => p.Id.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (installedPackageWithSameId != null)
                    {
                        packageTargetsForResolver.Remove(installedPackageWithSameId);
                        if (installedPackageWithSameId.Version > packageIdentity.Version)
                        {
                            // Looks like the installed package is of higher version than one being installed. So, we take it that downgrade is allowed
                            downgradeAllowed = true;
                        }
                    }
                    packageTargetsForResolver.Add(packageIdentity);

                    // Step-1 : Get metadata resources using gatherer
                    var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                    var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                    nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToGatherDependencyInfo, packageIdentity, projectName, targetFramework);

                    var primaryPackages = new List<PackageIdentity> { packageIdentity };

                    // If any targets are prerelease we should gather with prerelease on and filter afterwards
                    var includePrereleaseInGather = resolutionContext.IncludePrerelease || (packageTargetsForResolver.Any(p => (p.HasVersion && p.Version.IsPrerelease)));

                    var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherPackageDependencyInfo(
                        primaryPackages,
                        oldListOfInstalledPackages,
                        targetFramework,
                        primarySources,
                        effectiveSources,
                        PackagesFolderSourceRepository,
                        token);

                    if (!availablePackageDependencyInfoWithSourceSet.Any())
                    {
                        throw new InvalidOperationException(string.Format(Strings.UnableToGatherDependencyInfo, packageIdentity));
                    }

                    // Prune the results down to only what we would allow to be installed

                    // Keep only the target package we are trying to install for that Id
                    var prunedAvailablePackages = PrunePackageTree.RemoveAllVersionsForIdExcept(availablePackageDependencyInfoWithSourceSet, packageIdentity);

                    if (!resolutionContext.IncludePrerelease)
                    {
                        prunedAvailablePackages = PrunePackageTree.PrunePreleaseForStableTargets(prunedAvailablePackages, packageTargetsForResolver);
                    }

                    // Verify that the target is allowed by packages.config
                    ResolverGather.ThrowIfVersionIsDisallowedByPackagesConfig(packageIdentity.Id, projectInstalledPackageReferences, prunedAvailablePackages);

                    // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                    prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                    // Step-2 : Call PackageResolver.Resolve to get new list of installed packages

                    // Note: resolver prefers installed package versions if the satisfy the dependency version constraints
                    // So, since we want an exact version of a package, create a new list of installed packages where the packageIdentity being installed
                    // is present after removing the one with the same id
                    var preferredPackageReferences = new List<PackageReference>(projectInstalledPackageReferences.Where(pr =>
                        !pr.PackageIdentity.Id.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase)));
                    preferredPackageReferences.Add(new PackageReference(packageIdentity, targetFramework));

                    var packageResolverContext = new PackageResolverContext(resolutionContext.DependencyBehavior,
                        new string[] { packageIdentity.Id },
                        oldListOfInstalledPackages.Select(package => package.Id),
                        projectInstalledPackageReferences,
                        preferredPackageReferences.Select(package => package.PackageIdentity),
                        prunedAvailablePackages);

                    nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToResolveDependencies, packageIdentity, resolutionContext.DependencyBehavior);

                    var packageResolver = new PackageResolver();

                    var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);
                    if (newListOfInstalledPackages == null)
                    {
                        throw new InvalidOperationException(string.Format(Strings.UnableToResolveDependencyInfo, packageIdentity, resolutionContext.DependencyBehavior));
                    }

                    // Step-3 : Get the list of nuGetProjectActions to perform, install/uninstall on the nugetproject
                    // based on newPackages obtained in Step-2 and project.GetInstalledPackages                    

                    nuGetProjectContext.Log(MessageLevel.Info, Strings.ResolvingActionsToInstallPackage, packageIdentity);
                    var newPackagesToUninstall = new List<PackageIdentity>();
                    foreach (var oldInstalledPackage in oldListOfInstalledPackages)
                    {
                        var newPackageWithSameId = newListOfInstalledPackages
                            .Where(np => oldInstalledPackage.Id.Equals(np.Id, StringComparison.OrdinalIgnoreCase) &&
                                         !oldInstalledPackage.Version.Equals(np.Version)).FirstOrDefault();

                        if (newPackageWithSameId != null)
                        {
                            if (!downgradeAllowed
                                && oldInstalledPackage.Version > newPackageWithSameId.Version)
                            {
                                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.NewerVersionAlreadyReferenced, newPackageWithSameId.Id));
                            }
                            newPackagesToUninstall.Add(oldInstalledPackage);
                        }
                    }
                    var newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));

                    foreach (var newPackageToUninstall in newPackagesToUninstall)
                    {
                        nuGetProjectActions.Add(NuGetProjectAction.CreateUninstallProjectAction(newPackageToUninstall));
                    }

                    var comparer = PackageIdentity.Comparer;

                    foreach (var newPackageToInstall in newPackagesToInstall)
                    {
                        // find the package match based on identity
                        var sourceDepInfo = prunedAvailablePackages.Where(p => comparer.Equals(p, newPackageToInstall)).SingleOrDefault();

                        if (sourceDepInfo == null)
                        {
                            // this really should never happen
                            throw new InvalidOperationException(string.Format(Strings.PackageNotFound, packageIdentity));
                        }

                        nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(newPackageToInstall, sourceDepInfo.Source));
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (AggregateException aggregateEx)
                {
                    throw new InvalidOperationException(aggregateEx.Message, aggregateEx);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(ex.Message))
                    {
                        throw new InvalidOperationException(string.Format(Strings.PackageCouldNotBeInstalled, packageIdentity), ex);
                    }
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }
            else
            {
                var sourceRepository = await GetSourceRepository(packageIdentity, effectiveSources);
                nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(packageIdentity, sourceRepository));
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.ResolvedActionsToInstallPackage, packageIdentity);
            return nuGetProjectActions;
        }

        /// <summary>
        /// Check all sources in parallel to see if the package exists while respecting the order of the list.
        /// </summary>
        private static async Task<SourceRepository> GetSourceRepository(PackageIdentity packageIdentity, IEnumerable<SourceRepository> sourceRepositories)
        {
            SourceRepository source = null;

            // TODO: move this timeout to a better place
            // TODO: what should the timeout be?
            // Give up after 5 minutes
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var results = new Queue<KeyValuePair<SourceRepository, Task<bool>>>();

            foreach (var sourceRepository in sourceRepositories)
            {
                // TODO: fetch the resource in parallel also
                var metadataResource = await sourceRepository.GetResourceAsync<MetadataResource>();
                if (metadataResource != null)
                {
                    var task = Task.Run(() => metadataResource.Exists(packageIdentity, tokenSource.Token), tokenSource.Token);
                    results.Enqueue(new KeyValuePair<SourceRepository, Task<bool>>(sourceRepository, task));
                }
            }

            while (results.Count > 0)
            {
                try
                {
                    var pair = results.Dequeue();
                    var exists = await pair.Value;

                    // take only the first true result, but continue waiting for the remaining cancelled
                    // tasks to keep things from getting out of control.
                    if (source == null && exists)
                    {
                        source = pair.Key;

                        // there is no need to finish trying the others
                        tokenSource.Cancel();
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignore these
                }
                catch (Exception)
                {
                    Debug.Fail("Error finding repository");
                }
            }

            if (source == null)
            {
                // no matches were found
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownPackageSpecificVersion, packageIdentity.Id, packageIdentity.Version));
            }

            return source;
        }

        /// <summary>
        /// Gives the preview as a list of NuGetProjectActions that will be performed to uninstall
        /// <paramref name="packageId" /> into <paramref name="nuGetProject" />
        /// <paramref name="uninstallationContext" /> and <paramref name="nuGetProjectContext" /> are used in the
        /// process.
        /// </summary>
        public async Task<IEnumerable<NuGetProjectAction>> PreviewUninstallPackageAsync(NuGetProject nuGetProject, string packageId,
            UninstallationContext uninstallationContext, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (uninstallationContext == null)
            {
                throw new ArgumentNullException(nameof(uninstallationContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            // Step-1: Get the packageIdentity corresponding to packageId and check if it exists to be uninstalled
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);
            var packageReference = installedPackages
                .Where(pr => pr.PackageIdentity.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (packageReference == null
                || packageReference.PackageIdentity == null)
            {
                throw new ArgumentException(string.Format(Strings.PackageToBeUninstalledCouldNotBeFound,
                    packageId, nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name)));
            }

            return await PreviewUninstallPackageAsyncPrivate(nuGetProject, packageReference, uninstallationContext, nuGetProjectContext, token);
        }

        /// <summary>
        /// Gives the preview as a list of <see cref="NuGetProjectAction" /> that will be performed to uninstall
        /// <paramref name="packageIdentity" /> into <paramref name="nuGetProject" />
        /// <paramref name="uninstallationContext" /> and <paramref name="nuGetProjectContext" /> are used in the
        /// process.
        /// </summary>
        public async Task<IEnumerable<NuGetProjectAction>> PreviewUninstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity,
            UninstallationContext uninstallationContext, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (uninstallationContext == null)
            {
                throw new ArgumentNullException(nameof(uninstallationContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            // Step-1: Get the packageIdentity corresponding to packageId and check if it exists to be uninstalled
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);
            var packageReference = installedPackages
                .Where(pr => pr.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference == null
                || packageReference.PackageIdentity == null)
            {
                throw new ArgumentException(string.Format(Strings.PackageToBeUninstalledCouldNotBeFound,
                    packageIdentity.Id, nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name)));
            }

            return await PreviewUninstallPackageAsyncPrivate(nuGetProject, packageReference, uninstallationContext, nuGetProjectContext, token);
        }

        private async Task<IEnumerable<NuGetProjectAction>> PreviewUninstallPackageAsyncPrivate(NuGetProject nuGetProject, PackageReference packageReference,
            UninstallationContext uninstallationContext, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (SolutionManager == null)
            {
                throw new InvalidOperationException(Strings.SolutionManagerNotAvailableForUninstall);
            }

            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateUninstallProjectAction(packageReference.PackageIdentity);
                return new[] { action };
            }

            // Step-1 : Get the metadata resources from "packages" folder or custom repository path
            var packageIdentity = packageReference.PackageIdentity;
            var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            var packageReferenceTargetFramework = packageReference.TargetFramework;
            nuGetProjectContext.Log(MessageLevel.Info, Strings.AttemptingToGatherDependencyInfo, packageIdentity, projectName, packageReferenceTargetFramework);

            // TODO: IncludePrerelease is a big question mark
            var installedPackageIdentities = (await nuGetProject.GetInstalledPackagesAsync(token)).Select(pr => pr.PackageIdentity);
            var dependencyInfoFromPackagesFolder = await GetDependencyInfoFromPackagesFolder(installedPackageIdentities,
                packageReferenceTargetFramework);

            nuGetProjectContext.Log(MessageLevel.Info, Strings.ResolvingActionsToUninstallPackage, packageIdentity);
            // Step-2 : Determine if the package can be uninstalled based on the metadata resources
            var packagesToBeUninstalled = UninstallResolver.GetPackagesToBeUninstalled(packageIdentity, dependencyInfoFromPackagesFolder, installedPackageIdentities, uninstallationContext);

            var nuGetProjectActions = packagesToBeUninstalled.Select(p => NuGetProjectAction.CreateUninstallProjectAction(p));

            nuGetProjectContext.Log(MessageLevel.Info, Strings.ResolvedActionsToUninstallPackage, packageIdentity);
            return nuGetProjectActions;
        }

        private async Task<IEnumerable<PackageDependencyInfo>> GetDependencyInfoFromPackagesFolder(IEnumerable<PackageIdentity> packageIdentities,
            NuGetFramework nuGetFramework)
        {
            try
            {
                var results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

                var dependencyInfoResource = await PackagesFolderSourceRepository.GetResourceAsync<DependencyInfoResource>();

                foreach (var package in packageIdentities)
                {
                    var packageDependencyInfo = await dependencyInfoResource.ResolvePackage(package, nuGetFramework, CancellationToken.None);
                    if (packageDependencyInfo != null)
                    {
                        results.Add(packageDependencyInfo);
                    }
                }

                return results;
            }
            catch (NuGetProtocolException)
            {
                return null;
            }
        }

        /// <summary>
        /// Executes the list of <paramref name="nuGetProjectActions" /> on <paramref name="nuGetProject" /> , which is
        /// likely obtained by calling into
        /// <see
        ///     cref="PreviewInstallPackageAsync(NuGetProject,string,ResolutionContext,INuGetProjectContext,SourceRepository,IEnumerable{SourceRepository},CancellationToken)" />
        /// <paramref name="nuGetProjectContext" /> is used in the process.
        /// </summary>
        public async Task ExecuteNuGetProjectActionsAsync(NuGetProject nuGetProject, IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (nuGetProjectActions == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectActions));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            // DNU: Find the closure before executing the actions
            var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
            if (buildIntegratedProject != null)
            {
                await ExecuteBuildIntegratedProjectActionsAsync(buildIntegratedProject,
                    nuGetProjectActions,
                    nuGetProjectContext,
                    token);
            }
            else
            {
                Exception executeNuGetProjectActionsException = null;
                var executedNuGetProjectActions = new Stack<NuGetProjectAction>();
                var packageWithDirectoriesToBeDeleted = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                try
                {
                    await nuGetProject.PreProcessAsync(nuGetProjectContext, token);
                    foreach (var nuGetProjectAction in nuGetProjectActions)
                    {
                        executedNuGetProjectActions.Push(nuGetProjectAction);
                        if (nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Uninstall)
                        {
                            await ExecuteUninstallAsync(nuGetProject, nuGetProjectAction.PackageIdentity, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                        }
                        else
                        {
                            using (var targetPackageStream = new MemoryStream())
                            {
                                await PackageDownloader.GetPackageStreamAsync(nuGetProjectAction.SourceRepository, nuGetProjectAction.PackageIdentity, targetPackageStream, token);
                                await ExecuteInstallAsync(nuGetProject, nuGetProjectAction.PackageIdentity, targetPackageStream, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                            }
                        }

                        var toFromString = nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Install ? Strings.To : Strings.From;
                        nuGetProjectContext.Log(MessageLevel.Info, Strings.SuccessfullyExecutedPackageAction,
                            nuGetProjectAction.NuGetProjectActionType.ToString().ToLowerInvariant(), nuGetProjectAction.PackageIdentity.ToString(), toFromString + " " + nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                    }
                    await nuGetProject.PostProcessAsync(nuGetProjectContext, token);

                    await OpenReadmeFile(nuGetProjectContext, token);
                }
                catch (Exception ex)
                {
                    executeNuGetProjectActionsException = ex;
                }

                if (executeNuGetProjectActionsException != null)
                {
                    await Rollback(nuGetProject, executedNuGetProjectActions, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                }

                // Delete the package directories as the last step, so that, if an uninstall had to be rolled back, we can just use the package file on the directory
                // Also, always perform deletion of package directories, even in a rollback, so that there are no stale package directories
                foreach (var packageWithDirectoryToBeDeleted in packageWithDirectoriesToBeDeleted)
                {
                    await DeletePackage(packageWithDirectoryToBeDeleted, nuGetProjectContext, token);
                }

                // Clear direct install
                SetDirectInstall(null, nuGetProjectContext);

                if (executeNuGetProjectActionsException != null)
                {
                    throw executeNuGetProjectActionsException;
                }
            }
        }

        /// <summary>
        /// Run project actions for build integrated projects.
        /// </summary>
        public async Task ExecuteBuildIntegratedProjectActionsAsync(
            BuildIntegratedNuGetProject buildIntegratedProject,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext, 
            CancellationToken token)
        {
            // Find the target framework
            var projectFramework = buildIntegratedProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);

            // Find all sources used in the project actions
            var sources = new HashSet<string>(
                nuGetProjectActions.Where(action => action.SourceRepository != null)
                    .Select(action => action.SourceRepository.PackageSource.Source),
                    StringComparer.OrdinalIgnoreCase);

            // Add all enabled sources for the existing packages
            var enabledSources = SourceRepositoryProvider.GetRepositories()
                .Select(repo => repo.PackageSource.Source);

            sources.UnionWith(enabledSources);

            // Read project.json
            JObject rawPackageSpec;
            using (var streamReader = new StreamReader(buildIntegratedProject.JsonConfigPath))
            {
                var reader = new JsonTextReader(streamReader);
                rawPackageSpec = JObject.Load(reader);
            }

            // Modify the package spec
            foreach (var action in nuGetProjectActions)
            {
                if (action.NuGetProjectActionType == NuGetProjectActionType.Uninstall)
                {
                    JsonConfigUtility.RemoveDependency(rawPackageSpec, action.PackageIdentity.Id);
                }
                else if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                {
                    JsonConfigUtility.AddDependency(rawPackageSpec, action.PackageIdentity);
                }
            }

            // Create a package spec from the modified json
            var packageSpec = JsonPackageSpecReader.GetPackageSpec(rawPackageSpec.ToString(),
                buildIntegratedProject.ProjectName,
                buildIntegratedProject.JsonConfigPath);

            // Restore based on the modified package spec. This operation does not write the lock file to disk.
            var restoreResult = await BuildIntegratedRestoreUtility.RestoreAsync(buildIntegratedProject, packageSpec, nuGetProjectContext, sources, token);

            if (restoreResult.Success)
            {
                // Write out project.json 
                // This can be replaced with the PackageSpec writer once it has been added to the library
                using (var writer = new StreamWriter(buildIntegratedProject.JsonConfigPath, append: false, encoding: Encoding.UTF8))
                {
                    await writer.WriteAsync(rawPackageSpec.ToString());
                }

                // Write out the lock file
                var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);
                var lockFileFormat = new LockFileFormat();
                lockFileFormat.Write(lockFilePath, restoreResult.LockFile);

                // Write out a message for each action
                foreach (var action in nuGetProjectActions)
                {
                    var identityString = String.Format(CultureInfo.InvariantCulture, "{0} {1}",
                        action.PackageIdentity.Id,
                        action.PackageIdentity.Version.ToNormalizedString());

                    var toFromString = action.NuGetProjectActionType == NuGetProjectActionType.Install ? Strings.To : Strings.From;
                    nuGetProjectContext.Log(MessageLevel.Info, Strings.SuccessfullyExecutedPackageAction,
                        action.NuGetProjectActionType.ToString().ToLowerInvariant(), identityString,
                        String.Format(CultureInfo.InvariantCulture, "{0} {1}", toFromString,
                            buildIntegratedProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name)));
                }
            }
            else
            {
                // Fail and display a rollback message to let the user know they have returned to the original state
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, Strings.RestoreFailedRollingBack, buildIntegratedProject.ProjectName));
            }
        }

        private async Task Rollback(NuGetProject nuGetProject, Stack<NuGetProjectAction> executedNuGetProjectActions, HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (executedNuGetProjectActions.Count > 0)
            {
                // Only print the rollback warning if we have something to rollback
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_RollingBack);
            }

            while (executedNuGetProjectActions.Count > 0)
            {
                var nuGetProjectAction = executedNuGetProjectActions.Pop();
                try
                {
                    if (nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        // Rolling back an install would be to uninstall the package
                        await ExecuteUninstallAsync(nuGetProject, nuGetProjectAction.PackageIdentity, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                    }
                    else
                    {
                        packageWithDirectoriesToBeDeleted.Remove(nuGetProjectAction.PackageIdentity);
                        var packagePath = PackagesFolderNuGetProject.GetInstalledPackageFilePath(nuGetProjectAction.PackageIdentity);
                        if (File.Exists(packagePath))
                        {
                            using (var packageStream = File.OpenRead(packagePath))
                            {
                                await ExecuteInstallAsync(nuGetProject, nuGetProjectAction.PackageIdentity, packageStream, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // TODO: We are ignoring exceptions on rollback. Is this OK?
                }
            }
        }

        private Task OpenReadmeFile(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var executionContext = nuGetProjectContext.ExecutionContext;
            if (executionContext != null
                && executionContext.DirectInstall != null)
            {
                var packagePath = PackagesFolderNuGetProject.GetInstalledPackageFilePath(executionContext.DirectInstall);
                if (File.Exists(packagePath))
                {
                    var readmeFilePath = Path.Combine(Path.GetDirectoryName(packagePath), Constants.ReadmeFileName);
                    if (File.Exists(readmeFilePath) && !token.IsCancellationRequested)
                    {
                        return executionContext.OpenFile(readmeFilePath);
                    }
                }
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// RestorePackage is only allowed on a folderNuGetProject. In most cases, one will simply use the
        /// packagesFolderPath from NuGetPackageManager
        /// to create a folderNuGetProject before calling into this method
        /// </summary>
        public async Task<bool> RestorePackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> sourceRepositories, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (PackageExistsInPackagesFolder(packageIdentity))
            {
                return false;
            }

            token.ThrowIfCancellationRequested();
            nuGetProjectContext.Log(MessageLevel.Info, string.Format(Strings.RestoringPackage, packageIdentity));
            var enabledSources = (sourceRepositories != null && sourceRepositories.Any()) ? sourceRepositories :
                SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);
            var sourceRepository = await GetSourceRepository(packageIdentity, enabledSources);

            token.ThrowIfCancellationRequested();
            using (var targetPackageStream = new MemoryStream())
            {
                await PackageDownloader.GetPackageStreamAsync(sourceRepository, packageIdentity, targetPackageStream, token);
                // If you already downloaded the package, just restore it, don't cancel the operation now
                await PackagesFolderNuGetProject.InstallPackageAsync(packageIdentity, targetPackageStream, nuGetProjectContext, token);
            }

            return true;
        }

        public Task<bool> CopySatelliteFilesAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            return PackagesFolderNuGetProject.CopySatelliteFilesAsync(packageIdentity, nuGetProjectContext, token);
        }

        public bool PackageExistsInPackagesFolder(PackageIdentity packageIdentity)
        {
            return PackagesFolderNuGetProject.PackageExists(packageIdentity);
        }

        private static Task ExecuteInstallAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity, Stream packageStream, HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // TODO: MinClientVersion check should be performed in preview. Can easily avoid a lot of rollback
            MinClientVersionHandler.CheckMinClientVersion(packageStream, packageIdentity);

            packageWithDirectoriesToBeDeleted.Remove(packageIdentity);
            return nuGetProject.InstallPackageAsync(packageIdentity, packageStream, nuGetProjectContext, token);

            // TODO: Consider using CancelEventArgs instead of a regular EventArgs??
            //if (packageOperationEventArgs.Cancel)
            //{
            //    return;
            //}
        }

        private async Task ExecuteUninstallAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity, HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Step-1: Call nuGetProject.UninstallPackage
            await nuGetProject.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

            // Step-2: Check if the package directory could be deleted
            if (!(nuGetProject is INuGetIntegratedProject)
                && !await PackageExistsInAnotherNuGetProject(nuGetProject, packageIdentity, SolutionManager, token))
            {
                packageWithDirectoriesToBeDeleted.Add(packageIdentity);
            }

            // TODO: Consider using CancelEventArgs instead of a regular EventArgs??
            //if (packageOperationEventArgs.Cancel)
            //{
            //    return;
            //}
        }

        /// <summary>
        /// Checks if package <paramref name="packageIdentity" /> that is installed in
        /// project <paramref name="nuGetProject" /> is also installed in any
        /// other projects in the solution.
        /// </summary>
        public static async Task<bool> PackageExistsInAnotherNuGetProject(NuGetProject nuGetProject, PackageIdentity packageIdentity, ISolutionManager solutionManager, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            var nuGetProjectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            foreach (var otherNuGetProject in solutionManager.GetNuGetProjects())
            {
                var otherNuGetProjectName = NuGetProject.GetUniqueNameOrName(otherNuGetProject);
                if (!otherNuGetProjectName.Equals(nuGetProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    var packageExistsInAnotherNuGetProject = (await otherNuGetProject.GetInstalledPackagesAsync(token)).Any(pr => pr.PackageIdentity.Equals(packageIdentity));
                    if (packageExistsInAnotherNuGetProject)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<bool> DeletePackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            // 1. Check if the Package exists at root, if not, return false
            if (!PackagesFolderNuGetProject.PackageExists(packageIdentity))
            {
                nuGetProjectContext.Log(MessageLevel.Warning, ProjectManagement.Strings.PackageDoesNotExistInFolder, packageIdentity, PackagesFolderNuGetProject.Root);
                return false;
            }

            nuGetProjectContext.Log(MessageLevel.Info, ProjectManagement.Strings.RemovingPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            // 2. Delete the package folder and files from the root directory of this FileSystemNuGetProject
            // Remember that the following code may throw System.UnauthorizedAccessException
            await PackagesFolderNuGetProject.DeletePackage(packageIdentity, nuGetProjectContext, token);
            nuGetProjectContext.Log(MessageLevel.Info, ProjectManagement.Strings.RemovedPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            return true;
        }

        public static Task<NuGetVersion> GetLatestVersionAsync(string packageId, ResolutionContext resolutionContext, SourceRepository primarySourceRepository, CancellationToken token)
        {
            return GetLatestVersionAsync(packageId, resolutionContext, new List<SourceRepository> { primarySourceRepository }, token);
        }

        private static async Task<NuGetVersion> GetLatestVersionAsync(string packageId, ResolutionContext resolutionContext,
            IEnumerable<SourceRepository> sources, CancellationToken token)
        {
            var tasks = new List<Task<NuGetVersion>>();

            foreach (var source in sources)
            {
                tasks.Add(Task.Run(async () => await GetLatestVersionCoreAsync(packageId, resolutionContext, source, token)));
            }

            var versions = await Task.WhenAll(tasks);
            return versions.Where(v => v != null).Max();
        }

        private static async Task<NuGetVersion> GetLatestVersionCoreAsync(string packageId, ResolutionContext resolutionContext, SourceRepository source, CancellationToken token)
        {
            NuGetVersion latestVersion = null;

            var metadataResource = await source.GetResourceAsync<MetadataResource>();

            if (metadataResource != null)
            {
                latestVersion = await metadataResource.GetLatestVersion(packageId,
                    resolutionContext.IncludePrerelease, resolutionContext.IncludeUnlisted, token);
            }

            return latestVersion;
        }

        private IEnumerable<SourceRepository> GetEffectiveSources(IEnumerable<SourceRepository> primarySources, IEnumerable<SourceRepository> secondarySources)
        {
            // Always have to add the packages folder as the primary repository so that
            // dependency info for an installed package that is unlisted from the server is still available :(
            var effectiveSources = new List<SourceRepository>(primarySources);
            effectiveSources.Add(PackagesFolderSourceRepository);
            effectiveSources.AddRange(secondarySources);

            return new HashSet<SourceRepository>(effectiveSources, new SourceRepositoryComparer());
        }

        public static void SetDirectInstall(PackageIdentity directInstall,
            INuGetProjectContext nuGetProjectContext)
        {
            if (directInstall != null
                && nuGetProjectContext != null
                && nuGetProjectContext.ExecutionContext != null)
            {
                var ideExecutionContext = nuGetProjectContext.ExecutionContext as IDEExecutionContext;
                if (ideExecutionContext != null)
                {
                    ideExecutionContext.IDEDirectInstall = directInstall;
                }
            }
        }

        public static void ClearDirectInstall(INuGetProjectContext nuGetProjectContext)
        {
            if (nuGetProjectContext != null
                && nuGetProjectContext.ExecutionContext != null)
            {
                var ideExecutionContext = nuGetProjectContext.ExecutionContext as IDEExecutionContext;
                if (ideExecutionContext != null)
                {
                    ideExecutionContext.IDEDirectInstall = null;
                }
            }
        }
    }
}
