// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
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
        private ISourceRepositoryProvider SourceRepositoryProvider { get; }

        private ISolutionManager SolutionManager { get; }

        private Configuration.ISettings Settings { get; }

        public IDeleteOnRestartManager DeleteOnRestartManager { get; }

        public FolderNuGetProject PackagesFolderNuGetProject { get; set; }

        public SourceRepository PackagesFolderSourceRepository { get; set; }

        /// <summary>
        /// To construct a NuGetPackageManager that does not need a SolutionManager like NuGet.exe
        /// </summary>
        public NuGetPackageManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            string packagesFolderPath)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositoryProvider));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (packagesFolderPath == null)
            {
                throw new ArgumentNullException(nameof(packagesFolderPath));
            }

            SourceRepositoryProvider = sourceRepositoryProvider;
            Settings = settings;

            InitializePackagesFolderInfo(packagesFolderPath);
        }

        /// <summary>
        /// To construct a NuGetPackageManager with a mandatory SolutionManager lke VS
        /// </summary>
        public NuGetPackageManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager,
            IDeleteOnRestartManager deleteOnRestartManager)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositoryProvider));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            if (deleteOnRestartManager == null)
            {
                throw new ArgumentNullException(nameof(deleteOnRestartManager));
            }

            SourceRepositoryProvider = sourceRepositoryProvider;
            Settings = settings;
            SolutionManager = solutionManager;

            InitializePackagesFolderInfo(PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings));
            DeleteOnRestartManager = deleteOnRestartManager;
        }

        private void InitializePackagesFolderInfo(string packagesFolderPath)
        {
            PackagesFolderNuGetProject = new FolderNuGetProject(packagesFolderPath);
            // Capturing it locally is important since it allows for the instance to cache packages for the lifetime
            // of the closure \ NuGetPackageManager.
            var sharedPackageRepository = new SharedPackageRepository(packagesFolderPath);
            var packageSource = new V2PackageSource(packagesFolderPath, () => sharedPackageRepository);
            PackagesFolderSourceRepository = SourceRepositoryProvider.CreateRepository(packageSource);
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

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            NuGetProject nuGetProject,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: null,
                packageIdentity: null,
                nuGetProject: nuGetProject,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            string packageId,
            NuGetProject nuGetProject,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: packageId,
                packageIdentity: null,
                nuGetProject: nuGetProject,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            PackageIdentity packageIdentity,
            NuGetProject nuGetProject,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: null,
                packageIdentity: packageIdentity,
                nuGetProject: nuGetProject,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        private async Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
                string packageId,
                PackageIdentity packageIdentity,
                NuGetProject nuGetProject,
                ResolutionContext resolutionContext,
                INuGetProjectContext nuGetProjectContext,
                IEnumerable<SourceRepository> primarySources,
                IEnumerable<SourceRepository> secondarySources,
                CancellationToken token)
        {
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

            if (primarySources == null)
            {
                throw new ArgumentNullException(nameof(primarySources));
            }

            if (secondarySources == null)
            {
                throw new ArgumentNullException(nameof(secondarySources));
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            // DNX and BuildIntegrated projects are handled here
            if (nuGetProject is INuGetIntegratedProject)
            {
                var actions = new List<NuGetProjectAction>();

                if (packageIdentity == null && packageId == null)
                {
                    // Update-Package  all

                    var lowLevelActions = new List<NuGetProjectAction>();

                    foreach (var installedPackage in projectInstalledPackageReferences)
                    {
                        NuGetVersion latestVersion = await GetLatestVersionAsync(
                            installedPackage.PackageIdentity.Id,
                            resolutionContext,
                            primarySources,
                            token);

                        if (latestVersion != null && latestVersion > installedPackage.PackageIdentity.Version)
                        {
                            lowLevelActions.Add(NuGetProjectAction.CreateUninstallProjectAction(installedPackage.PackageIdentity));
                            lowLevelActions.Add(NuGetProjectAction.CreateInstallProjectAction(
                                new PackageIdentity(installedPackage.PackageIdentity.Id, latestVersion),
                                primarySources.FirstOrDefault()));
                        }
                    }

                    // If the update operation is a no-op there will be no project actions.
                    if (lowLevelActions.Any())
                    {
                        var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                        if (buildIntegratedProject != null)
                        {
                            // Create a build integrated action
                            var buildIntegratedAction =
                                await PreviewBuildIntegratedProjectActionsAsync(buildIntegratedProject, lowLevelActions, nuGetProjectContext, token);

                            actions.Add(buildIntegratedAction);
                        }
                        else
                        {
                            // Use the low level actions for projectK
                            actions = lowLevelActions;
                        }
                    }
                }
                else
                {
                    // Find the id from either the package identity or the packageId directly.
                    var installedPackageId = packageIdentity?.Id ?? packageId;

                    var installedPackageReference = projectInstalledPackageReferences
                            .Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, installedPackageId))
                            .FirstOrDefault();

                    // Start with the given package identity if one was passed in
                    var updateToIdentity = packageIdentity;

                    // If the version was not passed in, find the highest version
                    if (updateToIdentity == null || !updateToIdentity.HasVersion)
                    {
                        // Step-1 : Get latest version for packageId
                        NuGetVersion latestVersion = await GetLatestVersionAsync(packageId, resolutionContext, primarySources, token);

                        if (latestVersion == null)
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownPackage, packageId));
                        }

                        if (installedPackageReference.PackageIdentity.Version > latestVersion)
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.NewerVersionAlreadyReferenced, updateToIdentity));
                        }

                        updateToIdentity = new PackageIdentity(packageId, latestVersion);
                    }

                    // No-op if this is not an actual update, or if the latest version from the source is less than the currently installed version
                    if (installedPackageReference != null && !(installedPackageReference.PackageIdentity.Equals(updateToIdentity)))
                    {
                        var action = NuGetProjectAction.CreateInstallProjectAction(updateToIdentity, primarySources.FirstOrDefault());

                        var lowLevelActions = new List<NuGetProjectAction>();
                        lowLevelActions.Add(NuGetProjectAction.CreateUninstallProjectAction(installedPackageReference.PackageIdentity));
                        lowLevelActions.Add(NuGetProjectAction.CreateInstallProjectAction(updateToIdentity, primarySources.FirstOrDefault()));

                        var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                        if (buildIntegratedProject != null)
                        {
                            // Create a build integrated action
                            var buildIntegratedAction =
                            await PreviewBuildIntegratedProjectActionsAsync(buildIntegratedProject, lowLevelActions, nuGetProjectContext, token);

                            actions.Add(buildIntegratedAction);
                        }
                        else
                        {
                            // Use the low level actions for projectK
                            actions = lowLevelActions;
                        }
                    }
                }

                return actions;
            }

            var preferredVersions = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);

            // By default we start by preferring everything we already have installed
            foreach (var installedPackage in oldListOfInstalledPackages)
            {
                preferredVersions[installedPackage.Id] = installedPackage;
            }

            var primaryTargetIds = Enumerable.Empty<string>();
            var primaryTargets = Enumerable.Empty<PackageIdentity>();

            // We have been given the exact PackageIdentity (id and version) to update to e.g. from PMC update-package -Id <id> -Version <version>
            if (packageIdentity != null)
            {
                primaryTargets = new[] { packageIdentity };
                primaryTargetIds = new[] { packageIdentity.Id };

                // If we have been given an explicit PackageIdentity to install then we will naturally prefer that
                preferredVersions[packageIdentity.Id] = packageIdentity;
            }
            // We have just been given the package id, in which case we will look for the highest version and attempt to move to that
            else if (packageId != null)
            {
                primaryTargetIds = new[] { packageId };

                // If we have been given just a package Id we certainly don't want the one installed - pruning will be significant
                preferredVersions.Remove(packageId);
            }
            // We are apply update logic to the complete project - attempting to resolver all updates together
            else
            {
                primaryTargetIds = projectInstalledPackageReferences.Select(p => p.PackageIdentity.Id);

                // We are performing a global project-wide update - nothing is preferred - again pruning will be significant
                preferredVersions.Clear();
            }

            // Note: resolver needs all the installed packages as targets too. And, metadata should be gathered for the installed packages as well
            var packageTargetIdsForResolver = new HashSet<string>(oldListOfInstalledPackages.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var packageIdToInstall in primaryTargetIds)
            {
                packageTargetIdsForResolver.Add(packageIdToInstall);
            }

            var nuGetProjectActions = new List<NuGetProjectAction>();
            if (!packageTargetIdsForResolver.Any())
            {
                return nuGetProjectActions;
            }

            try
            {
                // If any targets are prerelease we should gather with prerelease on and filter afterwards
                var includePrereleaseInGather = resolutionContext.IncludePrerelease || (projectInstalledPackageReferences.Any(p => (p.PackageIdentity.HasVersion && p.PackageIdentity.Version.IsPrerelease)));
                var contextForGather = new ResolutionContext(resolutionContext.DependencyBehavior, includePrereleaseInGather, resolutionContext.IncludeUnlisted, VersionConstraints.None);

                // Step-1 : Get metadata resources using gatherer
                var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.AttemptingToGatherDependencyInfoForMultiplePackages, projectName, targetFramework);

                var allSources = new List<SourceRepository>(primarySources);
                var primarySourcesSet = new HashSet<string>(primarySources.Select(s => s.PackageSource.Source));
                foreach (var secondarySource in secondarySources)
                {
                    if (!primarySourcesSet.Contains(secondarySource.PackageSource.Source))
                    {
                        allSources.Add(secondarySource);
                    }
                }

                // Unless the packageIdentity was explicitly asked for we should remove any potential downgrades
                var allowDowngrades = packageIdentity != null;

                var gatherContext = new GatherContext()
                {
                    InstalledPackages = oldListOfInstalledPackages.ToList(),
                    PrimaryTargetIds = primaryTargetIds.ToList(),
                    PrimaryTargets = primaryTargets.ToList(),
                    TargetFramework = targetFramework,
                    PrimarySources = primarySources.ToList(),
                    AllSources = allSources.ToList(),
                    PackagesFolderSource = PackagesFolderSourceRepository,
                    ResolutionContext = resolutionContext,
                    AllowDowngrades = allowDowngrades
                };

                var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherAsync(gatherContext, token);

                if (!availablePackageDependencyInfoWithSourceSet.Any())
                {
                    throw new InvalidOperationException(Strings.UnableToGatherDependencyInfoForMultiplePackages);
                }

                // Update-Package ALL packages scenarios must always include the packages in the current project
                // Scenarios include: (1) a package havign been deleted from a feed (2) a source being removed from nuget config (3) an explicitly specified source 
                if (packageId == null && packageIdentity == null)
                {
                    // BUG #1181 VS2015 : Updating from one feed fails for packages from different feed.

                    DependencyInfoResource packagesFolderResource = await PackagesFolderSourceRepository.GetResourceAsync<DependencyInfoResource>(token);
                    var packages = new List<SourcePackageDependencyInfo>();
                    foreach (var installedPackage in projectInstalledPackageReferences)
                    {
                        var packageInfo = await packagesFolderResource.ResolvePackage(installedPackage.PackageIdentity, targetFramework, token);
                        availablePackageDependencyInfoWithSourceSet.Add(packageInfo);
                    }
                }

                // Prune the results down to only what we would allow to be installed
                IEnumerable<SourcePackageDependencyInfo> prunedAvailablePackages = availablePackageDependencyInfoWithSourceSet;

                if (!resolutionContext.IncludePrerelease)
                {
                    prunedAvailablePackages = PrunePackageTree.PrunePreleaseForStableTargets(
                        prunedAvailablePackages,
                        targets: Enumerable.Empty<PackageIdentity>(),
                        packagesToInstall: Enumerable.Empty<PackageIdentity>());
                }

                // Remove packages that do not meet the constraints specified in the UpdateConstrainst
                prunedAvailablePackages = PrunePackageTree.PruneByUpdateConstraints(prunedAvailablePackages, projectInstalledPackageReferences, resolutionContext.VersionConstraints);

                // Remove all but the highest packages that are of the same Id as a specified packageId
                if (packageId != null)
                {
                    prunedAvailablePackages = PrunePackageTree.PruneAllButHighest(prunedAvailablePackages, packageId);

                    // And then verify that the installed package is not already of a higher version - this check here ensures the user get's the right error message
                    GatherExceptionHelpers.ThrowIfNewerVersionAlreadyReferenced(packageId, projectInstalledPackageReferences, prunedAvailablePackages);
                }

                // Verify that the target is allowed by packages.config
                GatherExceptionHelpers.ThrowIfVersionIsDisallowedByPackagesConfig(primaryTargetIds, projectInstalledPackageReferences, prunedAvailablePackages);

                // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                // Remove packages that are of the same Id but different version than the primartTargets
                prunedAvailablePackages = PrunePackageTree.PruneByPrimaryTargets(prunedAvailablePackages, primaryTargets);

                // Unless the packageIdentity was explicitly asked for we should remove any potential downgrades
                if (!allowDowngrades)
                {
                    prunedAvailablePackages = PrunePackageTree.PruneDowngrades(prunedAvailablePackages, projectInstalledPackageReferences);
                }

                // Step-2 : Call PackageResolver.Resolve to get new list of installed packages
                var packageResolver = new PackageResolver();
                var packageResolverContext = new PackageResolverContext(
                    resolutionContext.DependencyBehavior,
                    primaryTargetIds,
                    packageTargetIdsForResolver,
                    projectInstalledPackageReferences,
                    preferredVersions.Values,
                    prunedAvailablePackages,
                    SourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource));

                nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.AttemptingToResolveDependenciesForMultiplePackages);
                var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);
                if (newListOfInstalledPackages == null)
                {
                    throw new InvalidOperationException(Strings.UnableToResolveDependencyInfoForMultiplePackages);
                }

                // if we have been asked for exact versions of packages then we should also force the uninstall/install of those packages (this corresponds to a -Reinstall)
                bool force = PrunePackageTree.IsExactVersion(resolutionContext.VersionConstraints);

                var installedPackagesInDependencyOrder = await GetInstalledPackagesInDependencyOrder(nuGetProject, token);

                nuGetProjectActions = GetProjectActionsForUpdate(newListOfInstalledPackages, installedPackagesInDependencyOrder, prunedAvailablePackages, nuGetProjectContext, force);
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

        private static List<NuGetProjectAction> GetProjectActionsForUpdate(
            IEnumerable<PackageIdentity> newListOfInstalledPackages,
            IEnumerable<PackageIdentity> oldListOfInstalledPackages,
            IEnumerable<SourcePackageDependencyInfo> availablePackageDependencyInfoWithSourceSet,
            INuGetProjectContext nuGetProjectContext,
            bool forceReinstall)
        {
            // Step-3 : Get the list of nuGetProjectActions to perform, install/uninstall on the nugetproject
            // based on newPackages obtained in Step-2 and project.GetInstalledPackages
            var nuGetProjectActions = new List<NuGetProjectAction>();
            nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.ResolvingActionsToInstallOrUpdateMultiplePackages);

            // we are reinstalling everything so we just take the ordering directly from the Resolver
            var newPackagesToUninstall = oldListOfInstalledPackages;
            var newPackagesToInstall = newListOfInstalledPackages;

            if (!forceReinstall)
            {
                newPackagesToUninstall = oldListOfInstalledPackages.Where(p => !newListOfInstalledPackages.Contains(p));
                newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));
            }

            foreach (var newPackageToUninstall in newPackagesToUninstall.Reverse())
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
        public async Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(NuGetProject nuGetProject, PackageIdentity packageIdentity,
            ResolutionContext resolutionContext, INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository, IEnumerable<SourceRepository> secondarySources, CancellationToken token)
        {
            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, primarySourceRepository);
                var actions = new[] { action };

                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                if (buildIntegratedProject != null)
                {
                    actions = new[] {
                        await PreviewBuildIntegratedProjectActionsAsync(buildIntegratedProject, actions, nuGetProjectContext, token)
                    };
                }

                return actions;
            }

            var primarySources = new List<SourceRepository> { primarySourceRepository };
            return await PreviewInstallPackageAsync(nuGetProject, packageIdentity, resolutionContext,
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

            // The following special case for ProjectK is not correct, if they used nuget.exe
            // and multiple repositories in the -Source switch
            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, primarySources.First());
                var actions = new[] { action };

                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                if (buildIntegratedProject != null)
                {
                    actions = new[] {
                        await PreviewBuildIntegratedProjectActionsAsync(buildIntegratedProject, actions, nuGetProjectContext, token)
                    };
                }

                return actions;
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
                    nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.AttemptingToGatherDependencyInfo, packageIdentity, projectName, targetFramework);

                    var primaryPackages = new List<PackageIdentity> { packageIdentity };

                    var gatherContext = new GatherContext()
                    {
                        InstalledPackages = oldListOfInstalledPackages.ToList(),
                        PrimaryTargets = primaryPackages,
                        TargetFramework = targetFramework,
                        PrimarySources = primarySources.ToList(),
                        AllSources = effectiveSources.ToList(),
                        PackagesFolderSource = PackagesFolderSourceRepository,
                        ResolutionContext = resolutionContext,
                        AllowDowngrades = downgradeAllowed
                    };

                    var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherAsync(gatherContext, token);

                    if (!availablePackageDependencyInfoWithSourceSet.Any())
                    {
                        throw new InvalidOperationException(string.Format(Strings.UnableToGatherDependencyInfo, packageIdentity));
                    }

                    // Prune the results down to only what we would allow to be installed

                    // Keep only the target package we are trying to install for that Id
                    var prunedAvailablePackages = PrunePackageTree.RemoveAllVersionsForIdExcept(availablePackageDependencyInfoWithSourceSet, packageIdentity);

                    if (!downgradeAllowed)
                    {
                        prunedAvailablePackages = PrunePackageTree.PruneDowngrades(prunedAvailablePackages, projectInstalledPackageReferences);
                    }

                    if (!resolutionContext.IncludePrerelease)
                    {
                        prunedAvailablePackages = PrunePackageTree.PrunePreleaseForStableTargets(
                            prunedAvailablePackages,
                            packageTargetsForResolver,
                            new[] { packageIdentity });
                    }

                    // Verify that the target is allowed by packages.config
                    GatherExceptionHelpers.ThrowIfVersionIsDisallowedByPackagesConfig(packageIdentity.Id, projectInstalledPackageReferences, prunedAvailablePackages);

                    // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                    prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                    // Step-2 : Call PackageResolver.Resolve to get new list of installed packages

                    // Note: resolver prefers installed package versions if the satisfy the dependency version constraints
                    // So, since we want an exact version of a package, create a new list of installed packages where the packageIdentity being installed
                    // is present after removing the one with the same id
                    var preferredPackageReferences = new List<Packaging.PackageReference>(projectInstalledPackageReferences.Where(pr =>
                        !pr.PackageIdentity.Id.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase)));
                    preferredPackageReferences.Add(new Packaging.PackageReference(packageIdentity, targetFramework));

                    var packageResolverContext = new PackageResolverContext(resolutionContext.DependencyBehavior,
                        new string[] { packageIdentity.Id },
                        oldListOfInstalledPackages.Select(package => package.Id),
                        projectInstalledPackageReferences,
                        preferredPackageReferences.Select(package => package.PackageIdentity),
                        prunedAvailablePackages,
                        SourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource));

                    nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.AttemptingToResolveDependencies, packageIdentity, resolutionContext.DependencyBehavior);

                    var packageResolver = new PackageResolver();

                    var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);
                    if (newListOfInstalledPackages == null)
                    {
                        throw new InvalidOperationException(string.Format(Strings.UnableToResolveDependencyInfo, packageIdentity, resolutionContext.DependencyBehavior));
                    }

                    // Step-3 : Get the list of nuGetProjectActions to perform, install/uninstall on the nugetproject
                    // based on newPackages obtained in Step-2 and project.GetInstalledPackages

                    nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvingActionsToInstallPackage, packageIdentity);
                    var newPackagesToUninstall = new List<PackageIdentity>();
                    foreach (var oldInstalledPackage in oldListOfInstalledPackages)
                    {
                        var newPackageWithSameId = newListOfInstalledPackages
                            .FirstOrDefault(np =>
                                oldInstalledPackage.Id.Equals(np.Id, StringComparison.OrdinalIgnoreCase) &&
                                !oldInstalledPackage.Version.Equals(np.Version));

                        if (newPackageWithSameId != null)
                        {
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
                        var sourceDepInfo = prunedAvailablePackages.SingleOrDefault(p => comparer.Equals(p, newPackageToInstall));

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

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvedActionsToInstallPackage, packageIdentity);
            return nuGetProjectActions;
        }

        /// <summary>
        /// Check all sources in parallel to see if the package exists while respecting the order of the list.
        /// This is only used by PreviewInstall with DependencyBehavior.Ignore.
        /// Since, resolver gather is not used when dependencies are not used,
        /// we simply get the source repository using MetadataResource.Exists
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
                catch (OperationCanceledException)
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
            var packageReference = installedPackages.FirstOrDefault(pr => pr.PackageIdentity.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (packageReference?.PackageIdentity == null)
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
            var packageReference = installedPackages.FirstOrDefault(pr => pr.PackageIdentity.Equals(packageIdentity));
            if (packageReference?.PackageIdentity == null)
            {
                throw new ArgumentException(string.Format(Strings.PackageToBeUninstalledCouldNotBeFound,
                    packageIdentity.Id, nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name)));
            }

            return await PreviewUninstallPackageAsyncPrivate(nuGetProject, packageReference, uninstallationContext, nuGetProjectContext, token);
        }

        private async Task<IEnumerable<NuGetProjectAction>> PreviewUninstallPackageAsyncPrivate(NuGetProject nuGetProject, Packaging.PackageReference packageReference,
            UninstallationContext uninstallationContext, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (SolutionManager == null)
            {
                throw new InvalidOperationException(Strings.SolutionManagerNotAvailableForUninstall);
            }

            if (nuGetProject is INuGetIntegratedProject)
            {
                var action = NuGetProjectAction.CreateUninstallProjectAction(packageReference.PackageIdentity);
                var actions = new[] { action };

                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                if (buildIntegratedProject != null)
                {
                    actions = new[] {
                        await PreviewBuildIntegratedProjectActionsAsync(buildIntegratedProject, actions, nuGetProjectContext, token)
                    };
                }

                return actions;
            }

            // Step-1 : Get the metadata resources from "packages" folder or custom repository path
            var packageIdentity = packageReference.PackageIdentity;
            var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            var packageReferenceTargetFramework = packageReference.TargetFramework;
            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.AttemptingToGatherDependencyInfo, packageIdentity, projectName, packageReferenceTargetFramework);

            // TODO: IncludePrerelease is a big question mark
            var installedPackageIdentities = (await nuGetProject.GetInstalledPackagesAsync(token)).Select(pr => pr.PackageIdentity);
            var dependencyInfoFromPackagesFolder = await GetDependencyInfoFromPackagesFolder(installedPackageIdentities,
                packageReferenceTargetFramework);

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvingActionsToUninstallPackage, packageIdentity);
            // Step-2 : Determine if the package can be uninstalled based on the metadata resources
            var packagesToBeUninstalled = UninstallResolver.GetPackagesToBeUninstalled(packageIdentity, dependencyInfoFromPackagesFolder, installedPackageIdentities, uninstallationContext);

            var nuGetProjectActions = packagesToBeUninstalled.Select(NuGetProjectAction.CreateUninstallProjectAction);

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvedActionsToUninstallPackage, packageIdentity);
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
                // Set the original packages config if it exists
                var msbuildProject = nuGetProject as MSBuildNuGetProject;
                if (msbuildProject != null)
                {
                    nuGetProjectContext.OriginalPackagesConfig = 
                        msbuildProject.PackagesConfigNuGetProject?.GetPackagesConfig();
                }

                ExceptionDispatchInfo exceptionInfo = null;
                var executedNuGetProjectActions = new Stack<NuGetProjectAction>();
                var packageWithDirectoriesToBeDeleted = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                var ideExecutionContext = nuGetProjectContext.ExecutionContext as IDEExecutionContext;
                if (ideExecutionContext != null)
                {
                    await ideExecutionContext.SaveExpandedNodeStates(SolutionManager);
                }

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
                            using (var downloadPackageResult = await PackageDownloader.GetDownloadResourceResultAsync(nuGetProjectAction.SourceRepository, nuGetProjectAction.PackageIdentity, Settings, token))
                            {
                                // use the version exactly as specified in the nuspec file
                                var packageIdentity = downloadPackageResult.PackageReader.GetIdentity();

                                await ExecuteInstallAsync(nuGetProject, packageIdentity, downloadPackageResult, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                            }
                        }

                        if (nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Install)
                        {
                            nuGetProjectContext.Log(
                                ProjectManagement.MessageLevel.Info,
                                Strings.SuccessfullyInstalled,
                                nuGetProjectAction.PackageIdentity,
                                nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                        }
                        else
                        {
                            // uninstall
                            nuGetProjectContext.Log(
                                ProjectManagement.MessageLevel.Info,
                                Strings.SuccessfullyUninstalled,
                                nuGetProjectAction.PackageIdentity,
                                nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                        }
                    }
                    await nuGetProject.PostProcessAsync(nuGetProjectContext, token);

                    await OpenReadmeFile(nuGetProjectContext, token);
                }
                catch (Exception ex)
                {
                    exceptionInfo = ExceptionDispatchInfo.Capture(ex);
                }

                if (exceptionInfo != null)
                {
                    await Rollback(nuGetProject, executedNuGetProjectActions, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
                }

                if (ideExecutionContext != null)
                {
                    await ideExecutionContext.CollapseAllNodes(SolutionManager);
                }

                // Delete the package directories as the last step, so that, if an uninstall had to be rolled back, we can just use the package file on the directory
                // Also, always perform deletion of package directories, even in a rollback, so that there are no stale package directories
                foreach (var packageWithDirectoryToBeDeleted in packageWithDirectoriesToBeDeleted)
                {
                    var packageFolderPath = PackagesFolderNuGetProject.GetInstalledPath(packageWithDirectoryToBeDeleted);
                    try
                    {
                        await DeletePackage(packageWithDirectoryToBeDeleted, nuGetProjectContext, token);
                    }
                    finally
                    {
                        if (DeleteOnRestartManager != null)
                        {
                            if (Directory.Exists(packageFolderPath))
                            {
                                DeleteOnRestartManager.MarkPackageDirectoryForDeletion(
                                    packageWithDirectoryToBeDeleted,
                                    packageFolderPath,
                                    nuGetProjectContext);

                                // Raise the event to notify listners to update the UI etc.
                                DeleteOnRestartManager.CheckAndRaisePackageDirectoriesMarkedForDeletion();
                            }
                        }
                    }
                }

                // Clear direct install
                SetDirectInstall(null, nuGetProjectContext);

                if (exceptionInfo != null)
                {
                    exceptionInfo.Throw();
                }
            }
        }

        /// <summary>
        /// Run project actions for build integrated projects.
        /// </summary>
        public async Task<BuildIntegratedProjectAction> PreviewBuildIntegratedProjectActionsAsync(
            BuildIntegratedNuGetProject buildIntegratedProject,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            if (nuGetProjectActions == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectActions));
            }

            if (buildIntegratedProject == null)
            {
                throw new ArgumentNullException(nameof(buildIntegratedProject));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (!nuGetProjectActions.Any())
            {
                // Return null if there are no actions.
                return null;
            }

            // Find all sources used in the project actions
            var sources = new HashSet<string>(
                nuGetProjectActions.Where(action => action.SourceRepository != null)
                    .Select(action => action.SourceRepository.PackageSource.Source),
                    StringComparer.OrdinalIgnoreCase);

            // Add all enabled sources for the existing packages
            var enabledSources = SourceRepositoryProvider.GetRepositories()
                .Select(repo => repo.PackageSource.Source);

            sources.UnionWith(enabledSources);

            // Read the current lock file if it exists
            LockFile originalLockFile = null;
            var lockFileFormat = new LockFileFormat();

            var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            if (File.Exists(lockFilePath))
            {
                originalLockFile = lockFileFormat.Read(lockFilePath);
            }

            // Read project.json
            JObject rawPackageSpec;
            using (var streamReader = new StreamReader(buildIntegratedProject.JsonConfigPath))
            {
                var reader = new JsonTextReader(streamReader);
                rawPackageSpec = JObject.Load(reader);
            }

            // For installs only use cache entries newer than the current time.
            // This is needed for scenarios where a new package shows up in search
            // but a previous cache entry does not yet have it.
            var cacheContext = new SourceCacheContext()
            {
                ListMaxAge = DateTimeOffset.UtcNow
            };

            var logger = new ProjectContextLogger(nuGetProjectContext);

            var effectiveGlobalPackagesFolder = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                                                    SolutionManager?.SolutionDirectory,
                                                    Settings);

            // If the lock file does not exist, restore before starting the operations
            if (originalLockFile == null)
            {
                var originalPackageSpec = JsonPackageSpecReader.GetPackageSpec(
                    rawPackageSpec.ToString(),
                    buildIntegratedProject.ProjectName,
                    buildIntegratedProject.JsonConfigPath);

                var originalRestoreResult = await BuildIntegratedRestoreUtility.RestoreAsync(
                    buildIntegratedProject,
                    originalPackageSpec,
                    logger,
                    sources,
                    effectiveGlobalPackagesFolder,
                    cacheContext,
                    token);

                originalLockFile = originalRestoreResult.LockFile;
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
            var restoreResult = await BuildIntegratedRestoreUtility.RestoreAsync(buildIntegratedProject,
                packageSpec,
                logger,
                sources,
                effectiveGlobalPackagesFolder,
                cacheContext,
                token);

            return new BuildIntegratedProjectAction(nuGetProjectActions.First().PackageIdentity,
                   nuGetProjectActions.First().NuGetProjectActionType,
                   originalLockFile,
                   rawPackageSpec,
                   restoreResult,
                   sources.ToList());
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
            BuildIntegratedProjectAction projectAction = null;

            if (nuGetProjectActions.Count() == 1
                && nuGetProjectActions.All(action => action is BuildIntegratedProjectAction))
            {
                projectAction = nuGetProjectActions.Single() as BuildIntegratedProjectAction;
            }
            else if (nuGetProjectActions.Any())
            {
                projectAction = await PreviewBuildIntegratedProjectActionsAsync(
                    buildIntegratedProject, 
                    nuGetProjectActions, 
                    nuGetProjectContext, 
                    token);
            }
            else
            {
                // There are no actions, this is a no-op
                return;
            }

            var actions = projectAction.GetProjectActions();

            // Check if all actions are uninstalls
            var uninstallOnly = projectAction.NuGetProjectActionType == NuGetProjectActionType.Uninstall
                && actions.All(action => action.NuGetProjectActionType == NuGetProjectActionType.Uninstall);

            var restoreResult = projectAction.RestoreResult;

            // Avoid committing the changes if the restore did not succeed
            // For uninstalls continue even if the restore failed to avoid blocking the user
            if (restoreResult.Success || uninstallOnly)
            {
                // Write out project.json
                // This can be replaced with the PackageSpec writer once it has been added to the library
                using (var writer = new StreamWriter(
                    buildIntegratedProject.JsonConfigPath,
                    append: false,
                    encoding: Encoding.UTF8))
                {
                    await writer.WriteAsync(projectAction.UpdatedProjectJson.ToString());
                }

                // Write out the lock file
                var logger = new ProjectContextLogger(nuGetProjectContext);
                restoreResult.Commit(logger);

                // Write out a message for each action
                foreach (var action in nuGetProjectActions)
                {
                    var identityString = String.Format(CultureInfo.InvariantCulture, "{0} {1}",
                        action.PackageIdentity.Id,
                        action.PackageIdentity.Version.ToNormalizedString());

                    if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        nuGetProjectContext.Log(
                            ProjectManagement.MessageLevel.Info,
                            Strings.SuccessfullyInstalled,
                            identityString,
                            buildIntegratedProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                    }
                    else
                    {
                        // uninstall 
                        nuGetProjectContext.Log(
                            ProjectManagement.MessageLevel.Info,
                            Strings.SuccessfullyUninstalled,
                            identityString,
                            buildIntegratedProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                    }
                }

                // Run init.ps1 scripts
                var sortedPackages =
                    BuildIntegratedProjectUtility.GetOrderedProjectDependencies(buildIntegratedProject);

                var addedPackages = new HashSet<PackageIdentity>(
                    BuildIntegratedRestoreUtility.GetAddedPackages(
                        projectAction.OriginalLockFile,
                        restoreResult.LockFile),
                    PackageIdentity.Comparer);

                var effectiveGlobalPackagesFolder = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                                                        SolutionManager?.SolutionDirectory,
                                                        Settings);

                // Find all dependencies in sorted order, then using the order run init.ps1 for only the new packages.
                foreach (var package in sortedPackages)
                {
                    if (addedPackages.Contains(package))
                    {
                        var packageInstallPath =
                            BuildIntegratedProjectUtility.GetPackagePathFromGlobalSource(
                                effectiveGlobalPackagesFolder,
                                package);

                        await buildIntegratedProject.ExecuteInitScriptAsync(
                            package,
                            packageInstallPath,
                            nuGetProjectContext,
                            false);
                    }
                }

                // Restore parent projects. These will be updated to include the transitive changes.
                var parents = await BuildIntegratedRestoreUtility.GetParentProjectsInClosure(SolutionManager, buildIntegratedProject);

                var cacheContext = new SourceCacheContext()
                {
                    ListMaxAge = DateTimeOffset.UtcNow
                };

                foreach (var parent in parents)
                {
                    // Restore and commit the lock file to disk regardless of the result
                    var parentResult = await BuildIntegratedRestoreUtility.RestoreAsync(
                        parent,
                        logger,
                        projectAction.Sources,
                        effectiveGlobalPackagesFolder,
                        cacheContext,
                        token);
                }
            }
            else
            {
                // Fail and display a rollback message to let the user know they have returned to the original state
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.RestoreFailedRollingBack,
                        buildIntegratedProject.ProjectName));
            }
        }

        private async Task Rollback(
            NuGetProject nuGetProject,
            Stack<NuGetProjectAction> executedNuGetProjectActions,
            HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            if (executedNuGetProjectActions.Count > 0)
            {
                // Only print the rollback warning if we have something to rollback
                nuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, Strings.Warning_RollingBack);
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
                            using (var downloadResourceResult = new DownloadResourceResult(File.OpenRead(packagePath)))
                            {
                                await ExecuteInstallAsync(nuGetProject, nuGetProjectAction.PackageIdentity, downloadResourceResult, packageWithDirectoriesToBeDeleted, nuGetProjectContext, token);
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
                    var readmeFilePath = Path.Combine(Path.GetDirectoryName(packagePath), ProjectManagement.Constants.ReadmeFileName);
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
            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, string.Format(Strings.RestoringPackage, packageIdentity));
            var enabledSources = (sourceRepositories != null && sourceRepositories.Any()) ? sourceRepositories :
                SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);

            token.ThrowIfCancellationRequested();

            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(enabledSources,
                packageIdentity,
                Settings,
                token))
            {
                packageIdentity = downloadResult.PackageReader.GetIdentity();

                // If you already downloaded the package, just restore it, don't cancel the operation now
                await PackagesFolderNuGetProject.InstallPackageAsync(packageIdentity, downloadResult, nuGetProjectContext, token);
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

        private static Task ExecuteInstallAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            DownloadResourceResult resourceResult,
            HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            // TODO: EnsurePackageCompatibility check should be performed in preview. Can easily avoid a lot of rollback
            EnsurePackageCompatibility(resourceResult, packageIdentity);

            packageWithDirectoriesToBeDeleted.Remove(packageIdentity);
            return nuGetProject.InstallPackageAsync(packageIdentity, resourceResult, nuGetProjectContext, token);
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
                // If the solution manager is null, simply assume that the
                // package exists on another nuget project to not delete it
                return true;
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
                nuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, ProjectManagement.Strings.PackageDoesNotExistInFolder, packageIdentity, PackagesFolderNuGetProject.Root);
                return false;
            }

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, ProjectManagement.Strings.RemovingPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            // 2. Delete the package folder and files from the root directory of this FileSystemNuGetProject
            // Remember that the following code may throw System.UnauthorizedAccessException
            await PackagesFolderNuGetProject.DeletePackage(packageIdentity, nuGetProjectContext, token);
            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, ProjectManagement.Strings.RemovedPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            return true;
        }

        public static Task<NuGetVersion> GetLatestVersionAsync(string packageId, ResolutionContext resolutionContext, SourceRepository primarySourceRepository, CancellationToken token)
        {
            return GetLatestVersionAsync(packageId, resolutionContext, new List<SourceRepository> { primarySourceRepository }, token);
        }

        public static async Task<NuGetVersion> GetLatestVersionAsync(
            string packageId,
            ResolutionContext resolutionContext,
            IEnumerable<SourceRepository> sources,
            CancellationToken token)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "Disposing the PackageReader will dispose the backing stream that we want to leave open.")]
        private static void EnsurePackageCompatibility(DownloadResourceResult downloadResourceResult, PackageIdentity packageIdentity)
        {
            NuGetVersion packageMinClientVersion;
            PackageType packageType;
            if (downloadResourceResult.PackageReader != null)
            {
                packageMinClientVersion = downloadResourceResult.PackageReader.GetMinClientVersion();
                packageType = downloadResourceResult.PackageReader.GetPackageType();
            }
            else
            {
                var packageZipArchive = new ZipArchive(downloadResourceResult.PackageStream, ZipArchiveMode.Read, leaveOpen: true);
                var packageReader = new PackageReader(packageZipArchive);
                var nuspecReader = new NuspecReader(packageReader.GetNuspec());
                packageMinClientVersion = nuspecReader.GetMinClientVersion();
                packageType = nuspecReader.GetPackageType();
            }

            // validate that the current version of NuGet satisfies the minVersion attribute specified in the .nuspec
            if (ProjectManagement.Constants.NuGetSemanticVersion < packageMinClientVersion)
            {
                throw new NuGetVersionNotSatisfiedException(
                    string.Format(CultureInfo.CurrentCulture, Strings.PackageMinVersionNotSatisfied,
                        packageIdentity.Id + " " + packageIdentity.Version.ToNormalizedString(),
                        packageMinClientVersion.ToNormalizedString(), ProjectManagement.Constants.NuGetSemanticVersion.ToNormalizedString()));
            }

            if (packageType != PackageType.Default)
            {
                throw new NuGetVersionNotSatisfiedException(
                    string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedPackageFeature,
                    packageIdentity.Id + " " + packageIdentity.Version.ToNormalizedString()));
            }
        }
    }
}