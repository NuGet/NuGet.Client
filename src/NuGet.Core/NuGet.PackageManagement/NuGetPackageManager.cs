// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol;
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
        private IReadOnlyList<SourceRepository> _globalPackageFolderRepositories;

        private ISourceRepositoryProvider SourceRepositoryProvider { get; }

        private ISolutionManager SolutionManager { get; }

        private Configuration.ISettings Settings { get; }

        private IDictionary<string, bool> _buildIntegratedProjectsUpdateDict;

        private DependencyGraphSpec _buildIntegratedProjectsCache;

        private RestoreCommandProvidersCache _restoreProviderCache;

        public IDeleteOnRestartManager DeleteOnRestartManager { get; }

        public FolderNuGetProject PackagesFolderNuGetProject { get; set; }

        public SourceRepository PackagesFolderSourceRepository { get; set; }

        public IInstallationCompatibility InstallationCompatibility { get; set; }

        /// <summary>
        /// Event to be raised when batch processing of install/ uninstall packages starts at a project level
        /// </summary>
        public event EventHandler<PackageProjectEventArgs> BatchStart;

        /// <summary>
        /// Event to be raised when batch processing of install/ uninstall packages ends at a project level
        /// </summary>
        public event EventHandler<PackageProjectEventArgs> BatchEnd;

        /// <summary>
        /// To construct a NuGetPackageManager that does not need a SolutionManager like NuGet.exe
        /// </summary>
        public NuGetPackageManager(
                ISourceRepositoryProvider sourceRepositoryProvider,
                Configuration.ISettings settings,
                string packagesFolderPath)
            : this(sourceRepositoryProvider, settings, packagesFolderPath, excludeVersion: false)
        {
        }

        public NuGetPackageManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            string packagesFolderPath,
            bool excludeVersion)
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
            InstallationCompatibility = PackageManagement.InstallationCompatibility.Instance;

            InitializePackagesFolderInfo(packagesFolderPath, excludeVersion);
        }

        /// <summary>
        /// To construct a NuGetPackageManager with a mandatory SolutionManager lke VS
        /// </summary>
        public NuGetPackageManager(
                ISourceRepositoryProvider sourceRepositoryProvider,
                Configuration.ISettings settings,
                ISolutionManager solutionManager,
                IDeleteOnRestartManager deleteOnRestartManager)
        : this(sourceRepositoryProvider, settings, solutionManager, deleteOnRestartManager, excludeVersion: false)
        {
        }

        public NuGetPackageManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager,
            IDeleteOnRestartManager deleteOnRestartManager,
            bool excludeVersion)
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
            InstallationCompatibility = PackageManagement.InstallationCompatibility.Instance;

            InitializePackagesFolderInfo(PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings), excludeVersion);
            DeleteOnRestartManager = deleteOnRestartManager;
        }

        /// <summary>
        /// SourceRepositories for the user global package folder and all fallback package folders.
        /// </summary>
        public IReadOnlyList<SourceRepository> GlobalPackageFolderRepositories
        {
            get
            {
                if (_globalPackageFolderRepositories == null)
                {
                    var sources = new List<SourceRepository>();

                    // Read package folders from settings
                    var pathContext = NuGetPathContext.Create(Settings);
                    var folders = new List<string>();
                    folders.Add(pathContext.UserPackageFolder);
                    folders.AddRange(pathContext.FallbackPackageFolders);

                    foreach (var folder in folders)
                    {
                        // Create a repo for each folder
                        var source = SourceRepositoryProvider.CreateRepository(
                            new PackageSource(folder),
                            FeedType.FileSystemV3);

                        sources.Add(source);
                    }

                    _globalPackageFolderRepositories = sources;
                }

                return _globalPackageFolderRepositories;
            }
        }

        private void InitializePackagesFolderInfo(string packagesFolderPath, bool excludeVersion = false)
        {
            // FileSystemPackagesConfig supports id.version formats, if the version is excluded use the normal v2 format
            var feedType = excludeVersion ? FeedType.FileSystemV2 : FeedType.FileSystemPackagesConfig;
            var resolver = new PackagePathResolver(packagesFolderPath, !excludeVersion);

            PackagesFolderNuGetProject = new FolderNuGetProject(packagesFolderPath, resolver);
            // Capturing it locally is important since it allows for the instance to cache packages for the lifetime
            // of the closure \ NuGetPackageManager.
            PackagesFolderSourceRepository = SourceRepositoryProvider.CreateRepository(
                new PackageSource(packagesFolderPath),
                feedType);
        }

        /// <summary>
        /// Installs the latest version of the given <paramref name="packageId" /> to NuGetProject
        /// <paramref name="nuGetProject" /> <paramref name="resolutionContext" /> and
        /// <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(
            NuGetProject nuGetProject,
            string packageId,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(sourceCacheContext);

                return InstallPackageAsync(
                    nuGetProject,
                    packageId,
                    resolutionContext,
                    nuGetProjectContext,
                    downloadContext,
                    new List<SourceRepository> { primarySourceRepository },
                    secondarySources,
                    token);
            }
        }

        /// <summary>
        /// Installs the latest version of the given <paramref name="packageId" /> to NuGetProject
        /// <paramref name="nuGetProject" /> <paramref name="resolutionContext" /> and
        /// <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(
            NuGetProject nuGetProject,
            string packageId,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return InstallPackageAsync(
                nuGetProject,
                packageId,
                resolutionContext,
                nuGetProjectContext,
                downloadContext,
                new List<SourceRepository> { primarySourceRepository },
                secondarySources,
                token);
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
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(sourceCacheContext);

                await InstallPackageAsync(
                    nuGetProject,
                    packageId,
                    resolutionContext,
                    nuGetProjectContext,
                    downloadContext,
                    primarySources,
                    secondarySources, token);
            }
        }

        /// <summary>
        /// Installs the latest version of the given
        /// <paramref name="packageId" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task InstallPackageAsync(
            NuGetProject nuGetProject,
            string packageId,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            var log = new LoggerAdapter(nuGetProjectContext);

            // Step-1 : Get latest version for packageId
            var resolvedPackage = await GetLatestVersionAsync(
                packageId,
                nuGetProject,
                resolutionContext,
                primarySources,
                log,
                token);

            if (resolvedPackage == null || resolvedPackage.LatestVersion == null)
            {
                throw new InvalidOperationException(string.Format(Strings.NoLatestVersionFound, packageId));
            }

            // Step-2 : Call InstallPackageAsync(project, packageIdentity)
            await InstallPackageAsync(
                nuGetProject,
                new PackageIdentity(packageId, resolvedPackage.LatestVersion),
                resolutionContext,
                nuGetProjectContext,
                downloadContext,
                primarySources,
                secondarySources,
                token);
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(sourceCacheContext);

                return InstallPackageAsync(
                    nuGetProject,
                    packageIdentity,
                    resolutionContext,
                    nuGetProjectContext,
                    downloadContext,
                    new List<SourceRepository> { primarySourceRepository },
                    secondarySources,
                    token);
            }
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public Task InstallPackageAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return InstallPackageAsync(
                nuGetProject,
                packageIdentity,
                resolutionContext,
                nuGetProjectContext,
                downloadContext,
                new List<SourceRepository> { primarySourceRepository },
                secondarySources,
                token);
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task InstallPackageAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(sourceCacheContext);

                await InstallPackageAsync(
                    nuGetProject,
                    packageIdentity,
                    resolutionContext,
                    nuGetProjectContext,
                    downloadContext,
                    primarySources,
                    secondarySources,
                    token);
            }
        }

        /// <summary>
        /// Installs given <paramref name="packageIdentity" /> to NuGetProject <paramref name="nuGetProject" />
        /// <paramref name="resolutionContext" /> and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task InstallPackageAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            ActivityCorrelationId.StartNew();

            // Step-1 : Call PreviewInstallPackageAsync to get all the nuGetProjectActions
            var nuGetProjectActions = await PreviewInstallPackageAsync(nuGetProject, packageIdentity, resolutionContext,
                nuGetProjectContext, primarySources, secondarySources, token);

            SetDirectInstall(packageIdentity, nuGetProjectContext);

            // Step-2 : Execute all the nuGetProjectActions
            await ExecuteNuGetProjectActionsAsync(
                nuGetProject,
                nuGetProjectActions,
                nuGetProjectContext,
                downloadContext,
                token);

            ClearDirectInstall(nuGetProjectContext);
        }

        public async Task UninstallPackageAsync(NuGetProject nuGetProject, string packageId, UninstallationContext uninstallationContext,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            ActivityCorrelationId.StartNew();

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
        public Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(
            NuGetProject nuGetProject,
            string packageId,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            SourceRepository primarySourceRepository,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewInstallPackageAsync(nuGetProject, packageId, resolutionContext, nuGetProjectContext, new[] { primarySourceRepository }, secondarySources, token);
        }

        /// <summary>
        /// Gives the preview as a list of NuGetProjectActions that will be performed to install
        /// <paramref name="packageId" /> into <paramref name="nuGetProject" /> <paramref name="resolutionContext" />
        /// and <paramref name="nuGetProjectContext" /> are used in the process.
        /// </summary>
        public async Task<IEnumerable<NuGetProjectAction>> PreviewInstallPackageAsync(
            NuGetProject nuGetProject,
            string packageId,
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

            var log = new LoggerAdapter(nuGetProjectContext);

            // Step-1 : Get latest version for packageId
            var resolvedPackage = await GetLatestVersionAsync(
                packageId,
                nuGetProject,
                resolutionContext,
                primarySources,
                log,
                token);

            if (resolvedPackage == null || resolvedPackage.LatestVersion == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownPackage, packageId));
            }

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var installedPackageReference = projectInstalledPackageReferences.Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, packageId)).FirstOrDefault();
            if (installedPackageReference != null
                && installedPackageReference.PackageIdentity.Version > resolvedPackage.LatestVersion)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.NewerVersionAlreadyReferenced, packageId));
            }

            // Step-2 : Call InstallPackage(project, packageIdentity)
            return await PreviewInstallPackageAsync(nuGetProject, new PackageIdentity(packageId, resolvedPackage.LatestVersion), resolutionContext,
                nuGetProjectContext, primarySources, secondarySources, token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            IEnumerable<NuGetProject> nuGetProjects,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: null,
                packageIdentities: new List<PackageIdentity>(),
                nuGetProjects: nuGetProjects,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            string packageId,
            IEnumerable<NuGetProject> nuGetProjects,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: packageId,
                packageIdentities: new List<PackageIdentity>(),
                nuGetProjects: nuGetProjects,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            PackageIdentity packageIdentity,
            IEnumerable<NuGetProject> nuGetProjects,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: null,
                packageIdentities: new List<PackageIdentity> { packageIdentity },
                nuGetProjects: nuGetProjects,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        public Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
            List<PackageIdentity> packageIdentities,
            IEnumerable<NuGetProject> nuGetProjects,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            return PreviewUpdatePackagesAsync(
                packageId: null,
                packageIdentities: packageIdentities,
                nuGetProjects: nuGetProjects,
                resolutionContext: resolutionContext,
                nuGetProjectContext: nuGetProjectContext,
                primarySources: primarySources,
                secondarySources: secondarySources,
                token: token);
        }

        private async Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesAsync(
                string packageId,
                List<PackageIdentity> packageIdentities,
                IEnumerable<NuGetProject> nuGetProjects,
                ResolutionContext resolutionContext,
                INuGetProjectContext nuGetProjectContext,
                IEnumerable<SourceRepository> primarySources,
                IEnumerable<SourceRepository> secondarySources,
                CancellationToken token)
        {
            if (nuGetProjects == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjects));
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

            var maxTasks = 4;
            var tasks = new List<Task<IEnumerable<NuGetProjectAction>>>(maxTasks);
            var nugetActions = new List<NuGetProjectAction>();

            var buildIntegratedProjects = nuGetProjects.OfType<BuildIntegratedNuGetProject>().ToList();
            var nonBuildIntegratedProjects = nuGetProjects.Except(buildIntegratedProjects).ToList();

            // add tasks for all build integrated projects
            foreach (var project in buildIntegratedProjects)
            {
                // if tasks count reachs max then wait until an existing task is completed
                if (tasks.Count >= maxTasks)
                {
                    var actions = await CompleteTaskAsync(tasks);
                    nugetActions.AddRange(actions);
                }

                // project.json based projects are handled here
                tasks.Add(Task.Run(async ()
                    => await PreviewUpdatePackagesForBuildIntegratedAsync(
                        packageId,
                        packageIdentities,
                        project,
                        resolutionContext,
                        nuGetProjectContext,
                        primarySources,
                        secondarySources,
                        token)));
            }

            // Wait for all restores to finish
            var allActions = await Task.WhenAll(tasks);
            nugetActions.AddRange(allActions.SelectMany(action => action));

            foreach (var project in nonBuildIntegratedProjects)
            {
                // packages.config based projects are handled here
                nugetActions.AddRange(await PreviewUpdatePackagesForClassicAsync(
                    packageId,
                    packageIdentities,
                    project,
                    resolutionContext,
                    nuGetProjectContext,
                    primarySources,
                    secondarySources,
                    token));
            }

            return nugetActions;
        }

        private async Task<IEnumerable<NuGetProjectAction>> CompleteTaskAsync(
            List<Task<IEnumerable<NuGetProjectAction>>> updateTasks)
        {
            var doneTask = await Task.WhenAny(updateTasks);
            updateTasks.Remove(doneTask);
            return await doneTask;
        }

        /// <summary>
        /// Update Package logic specific to build integrated style NuGet projects
        /// </summary>
        private async Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesForBuildIntegratedAsync(
            string packageId,
            List<PackageIdentity> packageIdentities,
            NuGetProject nuGetProject,
            ResolutionContext resolutionContext,
            INuGetProjectContext nuGetProjectContext,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> secondarySources,
            CancellationToken token)
        {
            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);

            var log = new LoggerAdapter(nuGetProjectContext);

            var actions = new List<NuGetProjectAction>();

            if (packageIdentities.Count == 0 && packageId == null)
            {
                // Update-Package  all

                //TODO: need to consider whether Update ALL simply does nothing for Build Integrated projects

                var lowLevelActions = new List<NuGetProjectAction>();

                foreach (var installedPackage in projectInstalledPackageReferences)
                {
                    // Skip auto referenced packages during update all.
                    var buildPackageReference = installedPackage as BuildIntegratedPackageReference;
                    var autoReferenced = buildPackageReference?.Dependency?.AutoReferenced == true;

                    if (!autoReferenced)
                    {
                        var resolvedPackage = await GetLatestVersionAsync(
                            installedPackage.PackageIdentity.Id,
                            nuGetProject,
                            resolutionContext,
                            primarySources,
                            log,
                            token);

                        if (resolvedPackage != null && resolvedPackage.LatestVersion != null && resolvedPackage.LatestVersion > installedPackage.PackageIdentity.Version)
                        {
                            lowLevelActions.Add(NuGetProjectAction.CreateUninstallProjectAction(installedPackage.PackageIdentity,
                                nuGetProject));
                            lowLevelActions.Add(NuGetProjectAction.CreateInstallProjectAction(
                                new PackageIdentity(installedPackage.PackageIdentity.Id, resolvedPackage.LatestVersion),
                                primarySources.FirstOrDefault(),
                                nuGetProject));
                        }
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
                // either we have a packageId or a list of specific PackageIdentities to work with

                // first lets normalize this input so we are just dealing with a list

                if (packageIdentities.Count == 0)
                {
                    var installedPackageReference = projectInstalledPackageReferences
                        .FirstOrDefault(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, packageId));

                    if (installedPackageReference != null)
                    {
                        var resolvedPackage = await GetLatestVersionAsync(
                            packageId,
                            nuGetProject,
                            resolutionContext,
                            primarySources,
                            log,
                            token);

                        if (resolvedPackage == null || !resolvedPackage.Exists)
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownPackage, packageId));
                        }
                        else if (resolvedPackage.LatestVersion != null)
                        {
                            if (installedPackageReference.PackageIdentity.Version > resolvedPackage.LatestVersion)
                            {
                                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                                    Strings.NewerVersionAlreadyReferenced, packageId));
                            }
                            else if (installedPackageReference.PackageIdentity.Version < resolvedPackage.LatestVersion)
                            {
                                packageIdentities.Add(new PackageIdentity(packageId, resolvedPackage.LatestVersion));
                            }
                        }
                    }
                }

                // process the list of PackageIdentities

                var lowLevelActions = new List<NuGetProjectAction>();

                foreach (var packageIdentity in packageIdentities)
                {
                    var installed = projectInstalledPackageReferences
                        .Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, packageIdentity.Id))
                        .FirstOrDefault();

                    //  if the package is not currently installed ignore it

                    if (installed != null)
                    {
                        lowLevelActions.Add(NuGetProjectAction.CreateUninstallProjectAction(installed.PackageIdentity, nuGetProject));
                        lowLevelActions.Add(NuGetProjectAction.CreateInstallProjectAction(packageIdentity,
                            primarySources.FirstOrDefault(), nuGetProject));
                    }
                }

                if (lowLevelActions.Count > 0)
                {
                    var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                    if (buildIntegratedProject != null)
                    {
                        // Create a build integrated action
                        var buildIntegratedAction = await PreviewBuildIntegratedProjectActionsAsync(
                            buildIntegratedProject,
                            lowLevelActions,
                            nuGetProjectContext,
                            token);

                        actions.Add(buildIntegratedAction);
                    }
                    else
                    {
                        // Use the low level actions for projectK
                        actions.AddRange(lowLevelActions);
                    }
                }
            }

            if (actions.Count == 0)
            {
                var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                nuGetProjectContext.Log(MessageLevel.Info, Strings.NoPackageUpdates, projectName);
            }

            return actions;
        }

        /// <summary>
        /// Update Package logic specific to classic style NuGet projects
        /// </summary>
        private async Task<IEnumerable<NuGetProjectAction>> PreviewUpdatePackagesForClassicAsync(
                string packageId,
                List<PackageIdentity> packageIdentities,
                NuGetProject nuGetProject,
                ResolutionContext resolutionContext,
                INuGetProjectContext nuGetProjectContext,
                IEnumerable<SourceRepository> primarySources,
                IEnumerable<SourceRepository> secondarySources,
                CancellationToken token)
        {
            var log = new LoggerAdapter(nuGetProjectContext);

            var projectId = string.Empty;
            nuGetProject.TryGetMetadata<string>(NuGetProjectMetadataKeys.ProjectId, out projectId);

            var telemetryHelper = nuGetProjectContext.TelemetryService;
            TelemetryServiceUtility.StartTimer(telemetryHelper);

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);

            var isUpdateAll = (packageId == null && packageIdentities.Count == 0);

            var preferredVersions = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);

            // By default we start by preferring everything we already have installed
            foreach (var installedPackage in oldListOfInstalledPackages)
            {
                preferredVersions[installedPackage.Id] = installedPackage;
            }

            var primaryTargetIds = new List<string>();
            var primaryTargets = new List<PackageIdentity>();

            // We have been given the exact PackageIdentities (id and version) to update to e.g. from PMC update-package -Id <id> -Version <version>
            if (packageIdentities.Count > 0)
            {
                // If we have been given explicit PackageIdentities to install then we will naturally prefer that
                foreach (var packageIdentity in packageIdentities)
                {
                    // Just a check to make sure the preferredVersions created from the existing package list actually contains the target
                    if (preferredVersions.ContainsKey(packageIdentity.Id))
                    {
                        primaryTargetIds.Add(packageIdentity.Id);

                        // If there was a version specified we will prefer that version
                        if (packageIdentity.HasVersion)
                        {
                            preferredVersions[packageIdentity.Id] = packageIdentity;
                            ((List<PackageIdentity>)primaryTargets).Add(packageIdentity);
                        }
                        // Otherwise we just have the Id and so we wil explicitly not prefer the one currently installed
                        else
                        {
                            preferredVersions.Remove(packageIdentity.Id);
                        }
                    }
                }
            }
            // We have just been given the package id, in which case we will look for the highest version and attempt to move to that
            else if (packageId != null)
            {
                if (preferredVersions.ContainsKey(packageId))
                {
                    if (PrunePackageTree.IsExactVersion(resolutionContext.VersionConstraints))
                    {
                        primaryTargets = new List<PackageIdentity> { preferredVersions[packageId] };
                    }
                    else
                    {
                        primaryTargetIds = new List<string> { packageId };

                        // If we have been given just a package Id we certainly don't want the one installed - pruning will be significant
                        preferredVersions.Remove(packageId);
                    }
                }
            }
            // We are apply update logic to the complete project - attempting to resolver all updates together
            else
            {
                primaryTargetIds = projectInstalledPackageReferences.Select(p => p.PackageIdentity.Id).ToList();

                // We are performing a global project-wide update - nothing is preferred - again pruning will be significant
                preferredVersions.Clear();
            }

            // Note: resolver needs all the installed packages as targets too. And, metadata should be gathered for the installed packages as well
            var packageTargetIdsForResolver = new HashSet<string>(oldListOfInstalledPackages.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var packageIdToInstall in primaryTargetIds)
            {
                packageTargetIdsForResolver.Add(packageIdToInstall);
            }

            var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            var nuGetProjectActions = new List<NuGetProjectAction>();
            if (!packageTargetIdsForResolver.Any())
            {
                nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.NoPackagesInProject, projectName);
                return nuGetProjectActions;
            }

            try
            {
                // If any targets are prerelease we should gather with prerelease on and filter afterwards
                var includePrereleaseInGather = resolutionContext.IncludePrerelease || (projectInstalledPackageReferences.Any(p => (p.PackageIdentity.HasVersion && p.PackageIdentity.Version.IsPrerelease)));

                // Create a modified resolution cache. This should include the same gather cache for multi-project
                // operations.
                var contextForGather = new ResolutionContext(
                    resolutionContext.DependencyBehavior,
                    includePrereleaseInGather,
                    resolutionContext.IncludeUnlisted,
                    VersionConstraints.None,
                    resolutionContext.GatherCache);

                // Step-1 : Get metadata resources using gatherer
                var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Environment.NewLine);
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
                var allowDowngrades = false;
                if (packageIdentities.Count == 1)
                {
                    // Get installed package version
                    var packageTargetsForResolver = new HashSet<PackageIdentity>(oldListOfInstalledPackages, PackageIdentity.Comparer);
                    var installedPackageWithSameId = packageTargetsForResolver.Where(p => p.Id.Equals(packageIdentities[0].Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (installedPackageWithSameId != null)
                    {
                        if (installedPackageWithSameId.Version > packageIdentities[0].Version)
                        {
                            // Looks like the installed package is of higher version than one being installed. So, we take it that downgrade is allowed
                            allowDowngrades = true;
                        }
                    }
                }

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
                    AllowDowngrades = allowDowngrades,
                    ProjectContext = nuGetProjectContext,
                    IsUpdateAll = isUpdateAll
                };

                var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherAsync(gatherContext, token);

                // emit gather dependency telemetry event and restart timer
                TelemetryServiceUtility.EmitEventAndRestartTimer(telemetryHelper,
                    string.Format(TelemetryConstants.GatherDependencyStepName, projectId));

                if (!availablePackageDependencyInfoWithSourceSet.Any())
                {
                    throw new InvalidOperationException(Strings.UnableToGatherDependencyInfoForMultiplePackages);
                }

                // Update-Package ALL packages scenarios must always include the packages in the current project
                // Scenarios include: (1) a package havign been deleted from a feed (2) a source being removed from nuget config (3) an explicitly specified source
                if (isUpdateAll)
                {
                    // BUG #1181 VS2015 : Updating from one feed fails for packages from different feed.

                    var packagesFolderResource = await PackagesFolderSourceRepository.GetResourceAsync<DependencyInfoResource>(token);
                    var packages = new List<SourcePackageDependencyInfo>();
                    foreach (var installedPackage in projectInstalledPackageReferences)
                    {
                        var packageInfo = await packagesFolderResource.ResolvePackage(installedPackage.PackageIdentity, targetFramework, log, token);
                        if (packageInfo != null)
                        {
                            availablePackageDependencyInfoWithSourceSet.Add(packageInfo);
                        }
                    }
                }

                // Prune the results down to only what we would allow to be installed
                IEnumerable<SourcePackageDependencyInfo> prunedAvailablePackages = availablePackageDependencyInfoWithSourceSet;

                if (!resolutionContext.IncludePrerelease)
                {
                    prunedAvailablePackages = PrunePackageTree.PrunePrereleaseExceptAllowed(
                        prunedAvailablePackages,
                        oldListOfInstalledPackages,
                        isUpdateAll);
                }

                // Remove packages that do not meet the constraints specified in the UpdateConstrainst
                prunedAvailablePackages = PrunePackageTree.PruneByUpdateConstraints(prunedAvailablePackages, projectInstalledPackageReferences, resolutionContext.VersionConstraints);

                // Verify that the target is allowed by packages.config
                GatherExceptionHelpers.ThrowIfVersionIsDisallowedByPackagesConfig(primaryTargetIds, projectInstalledPackageReferences, prunedAvailablePackages, log);

                // Remove versions that do not satisfy 'allowedVersions' attribute in packages.config, if any
                prunedAvailablePackages = PrunePackageTree.PruneDisallowedVersions(prunedAvailablePackages, projectInstalledPackageReferences);

                // Remove all but the highest packages that are of the same Id as a specified packageId
                if (packageId != null)
                {
                    prunedAvailablePackages = PrunePackageTree.PruneAllButHighest(prunedAvailablePackages, packageId);

                    // And then verify that the installed package is not already of a higher version - this check here ensures the user get's the right error message
                    GatherExceptionHelpers.ThrowIfNewerVersionAlreadyReferenced(packageId, projectInstalledPackageReferences, prunedAvailablePackages);
                }

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
                    SourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
                    log);

                nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.AttemptingToResolveDependenciesForMultiplePackages);
                var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);

                // emit resolve dependency telemetry event and restart timer
                TelemetryServiceUtility.EmitEventAndRestartTimer(telemetryHelper,
                    string.Format(TelemetryConstants.ResolveDependencyStepName, projectId));

                if (newListOfInstalledPackages == null)
                {
                    throw new InvalidOperationException(Strings.UnableToResolveDependencyInfoForMultiplePackages);
                }

                // if we have been asked for exact versions of packages then we should also force the uninstall/install of those packages (this corresponds to a -Reinstall)
                var isReinstall = PrunePackageTree.IsExactVersion(resolutionContext.VersionConstraints);

                var targetIds = Enumerable.Empty<string>();
                if (!isUpdateAll)
                {
                    targetIds = (isReinstall ? primaryTargets.Select(p => p.Id) : primaryTargetIds);
                }

                var installedPackagesInDependencyOrder = await GetInstalledPackagesInDependencyOrder(nuGetProject, token);

                var isDependencyBehaviorIgnore = resolutionContext.DependencyBehavior == DependencyBehavior.Ignore;

                nuGetProjectActions = GetProjectActionsForUpdate(
                    nuGetProject,
                    newListOfInstalledPackages,
                    installedPackagesInDependencyOrder,
                    prunedAvailablePackages,
                    nuGetProjectContext,
                    isReinstall,
                    targetIds,
                    isDependencyBehaviorIgnore);

                // emit resolve actions telemetry event
                TelemetryServiceUtility.EmitEvent(telemetryHelper,
                    string.Format(TelemetryConstants.ResolvedActionsStepName, projectId));

                if (nuGetProjectActions.Count == 0)
                {
                    nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.ResolutionSuccessfulNoAction);
                    nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.NoUpdatesAvailable);
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

            if (nuGetProjectActions.Count == 0)
            {
                nuGetProjectContext.Log(MessageLevel.Info, Strings.NoPackageUpdates, projectName);
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
            NuGetProject project,
            IEnumerable<PackageIdentity> newListOfInstalledPackages,
            IEnumerable<PackageIdentity> oldListOfInstalledPackages,
            IEnumerable<SourcePackageDependencyInfo> availablePackageDependencyInfoWithSourceSet,
            INuGetProjectContext nuGetProjectContext,
            bool isReinstall,
            IEnumerable<string> targetIds,
            bool isDependencyBehaviorIgnore)
        {
            // Step-3 : Get the list of nuGetProjectActions to perform, install/uninstall on the nugetproject
            // based on newPackages obtained in Step-2 and project.GetInstalledPackages
            var nuGetProjectActions = new List<NuGetProjectAction>();
            nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Strings.ResolvingActionsToInstallOrUpdateMultiplePackages);

            // we are reinstalling everything so we just take the ordering directly from the Resolver
            var newPackagesToUninstall = oldListOfInstalledPackages;
            var newPackagesToInstall = newListOfInstalledPackages;

            // we are doing a reinstall of a specific package - we will also want to generate Project Actions for the dependencies
            if (isReinstall && targetIds.Any())
            {
                var packageIdsToReinstall = new HashSet<string>(targetIds, StringComparer.OrdinalIgnoreCase);

                // Avoid getting dependencies if dependencyBehavior is set to ignore
                if (!isDependencyBehaviorIgnore)
                {
                    packageIdsToReinstall = GetDependencies(targetIds, newListOfInstalledPackages, availablePackageDependencyInfoWithSourceSet);
                }

                newPackagesToUninstall = oldListOfInstalledPackages.Where(p => packageIdsToReinstall.Contains(p.Id));
                newPackagesToInstall = newListOfInstalledPackages.Where(p => packageIdsToReinstall.Contains(p.Id));
            }

            if (!isReinstall)
            {
                if (targetIds.Any())
                {
                    // we are targeting a particular package - there is no need therefore to alter other aspects of the project
                    // specifically an unrelated package may have been force removed in which case we should be happy to leave things that way

                    // It will get the list of packages which are being uninstalled to get a new version
                    newPackagesToUninstall = oldListOfInstalledPackages.Where(oldPackage =>
                        newListOfInstalledPackages.Any(newPackage =>
                        StringComparer.OrdinalIgnoreCase.Equals(oldPackage.Id, newPackage.Id) && !oldPackage.Version.Equals(newPackage.Version)));

                    // this will be the new set of target ids which includes current target ids as well as packages which are being updated
                    //It fixes the issue where we were only getting dependencies for target ids ignoring other packages which are also being updated. #2724
                    var newTargetIds = new HashSet<string>(newPackagesToUninstall.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
                    newTargetIds.AddRange(targetIds);

                    var allowed = newTargetIds;

                    // Avoid getting dependencies if dependencyBehavior is set to ignore
                    if (!isDependencyBehaviorIgnore)
                    {
                        // first, we will allow all the dependencies of the package(s) beging targeted
                        allowed = GetDependencies(newTargetIds, newListOfInstalledPackages, availablePackageDependencyInfoWithSourceSet);
                    }

                    // second, any package that is currently in the solution will also be allowed to change
                    // (note this logically doesn't include packages that have been force uninstalled from the project
                    // because we wouldn't want to just add those back in)
                    foreach (var p in oldListOfInstalledPackages)
                    {
                        allowed.Add(p.Id);
                    }

                    newListOfInstalledPackages = newListOfInstalledPackages.Where(p => allowed.Contains(p.Id));
                    newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));
                }
                else
                {
                    newPackagesToUninstall = oldListOfInstalledPackages.Where(p => !newListOfInstalledPackages.Contains(p));
                    newPackagesToInstall = newListOfInstalledPackages.Where(p => !oldListOfInstalledPackages.Contains(p));
                }
            }

            foreach (var newPackageToUninstall in newPackagesToUninstall.Reverse())
            {
                nuGetProjectActions.Add(NuGetProjectAction.CreateUninstallProjectAction(newPackageToUninstall, project));
            }

            foreach (var newPackageToInstall in newPackagesToInstall)
            {
                // find the package match based on identity
                var sourceDepInfo = availablePackageDependencyInfoWithSourceSet.Where(p => PackageIdentity.Comparer.Equals(p, newPackageToInstall)).SingleOrDefault();

                if (sourceDepInfo == null)
                {
                    // this really should never happen
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentUICulture, Strings.PackageNotFound, newPackageToInstall));
                }

                nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(newPackageToInstall, sourceDepInfo.Source, project));
            }

            return nuGetProjectActions;
        }

        /// <summary>
        /// Filter down the reinstall list to just the ones we need to reinstall (i.e. the dependencies)
        /// </summary>
        private static HashSet<string> GetDependencies(IEnumerable<string> targetIds, IEnumerable<PackageIdentity> newListOfInstalledPackages, IEnumerable<SourcePackageDependencyInfo> available)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetId in targetIds)
            {
                CollectDependencies(result, targetId, newListOfInstalledPackages, available, 0);
            }
            return result;
        }

        /// <summary>
        /// A walk through the dependencies to collect the additional package identities that are involved in the current set of packages to be installed
        /// </summary>
        private static void CollectDependencies(HashSet<string> result,
            string id,
            IEnumerable<PackageIdentity> packages,
            IEnumerable<SourcePackageDependencyInfo> available, int depth)
        {
            // check if we've already added dependencies for current id
            if (!result.Add(id))
            {
                return;
            }

            // we want the exact PackageIdentity for this id
            var packageIdentity = packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (packageIdentity == null)
            {
                throw new ArgumentException("packages");
            }

            // now look up the dependencies of this exact package identity
            var sourceDepInfo = available.SingleOrDefault(p => packageIdentity.Equals(p));
            if (sourceDepInfo == null)
            {
                throw new ArgumentException("available");
            }

            // iterate through all the dependencies and call recursively to collect dependencies
            foreach (var dependency in sourceDepInfo.Dependencies)
            {
                // check we don't fall into an infinite loop caused by bad dependency data in the packages
                if (depth < packages.Count())
                {
                    CollectDependencies(result, dependency.Id, packages, available, depth + 1);
                }
            }
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
                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, primarySourceRepository, nuGetProject);
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

            if (nuGetProject is INuGetIntegratedProject)
            {
                SourceRepository sourceRepository;
                if (primarySources.Count() > 1)
                {
                    var logger = new ProjectContextLogger(nuGetProjectContext);
                    sourceRepository = await GetSourceRepository(packageIdentity, primarySources, logger);
                }
                else
                {
                    sourceRepository = primarySources.First();
                }

                var action = NuGetProjectAction.CreateInstallProjectAction(packageIdentity, sourceRepository, nuGetProject);
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

            var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            var projectId = string.Empty;
            nuGetProject.TryGetMetadata<string>(NuGetProjectMetadataKeys.ProjectId, out projectId);
            var telemetryHelper = nuGetProjectContext.TelemetryService;
            TelemetryServiceUtility.StartTimer(telemetryHelper);

            var projectInstalledPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
            var oldListOfInstalledPackages = projectInstalledPackageReferences.Select(p => p.PackageIdentity);
            if (oldListOfInstalledPackages.Any(p => p.Equals(packageIdentity)))
            {
                var alreadyInstalledMessage = string.Format(Strings.PackageAlreadyExistsInProject, packageIdentity, projectName ?? string.Empty);
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
                    var targetFramework = nuGetProject.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
                    nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Environment.NewLine);
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
                        AllowDowngrades = downgradeAllowed,
                        ProjectContext = nuGetProjectContext
                    };

                    var availablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherAsync(gatherContext, token);

                    // emit gather dependency telemetry event and restart timer
                    TelemetryServiceUtility.EmitEventAndRestartTimer(telemetryHelper,
                        string.Format(TelemetryConstants.GatherDependencyStepName, projectId));

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

                    var log = new LoggerAdapter(nuGetProjectContext);

                    // Verify that the target is allowed by packages.config
                    GatherExceptionHelpers.ThrowIfVersionIsDisallowedByPackagesConfig(packageIdentity.Id, projectInstalledPackageReferences, prunedAvailablePackages, log);

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
                        SourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
                        log);

                    nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.AttemptingToResolveDependencies, packageIdentity, resolutionContext.DependencyBehavior);

                    var packageResolver = new PackageResolver();

                    var newListOfInstalledPackages = packageResolver.Resolve(packageResolverContext, token);

                    // emit resolve dependency telemetry event and restart timer
                    TelemetryServiceUtility.EmitEventAndRestartTimer(telemetryHelper,
                        string.Format(TelemetryConstants.ResolveDependencyStepName, projectId));

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
                        nuGetProjectActions.Add(NuGetProjectAction.CreateUninstallProjectAction(newPackageToUninstall, nuGetProject));
                    }

                    // created hashset of packageIds we are OK with touching
                    // the scenario here is that the user might have done an uninstall-package -Force on a particular package

                    // this will be the new set of target ids which includes current target ids as well as packages which are being updated
                    //It fixes the issue where we were only getting dependencies for target ids ignoring other packages which are also being updated. #2724
                    var newTargetIds = new HashSet<string>(newPackagesToUninstall.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
                    newTargetIds.Add(packageIdentity.Id);

                    // get all dependencies of new target ids so that we can have all the required install actions.
                    var allowed = GetDependencies(newTargetIds, newListOfInstalledPackages, prunedAvailablePackages);

                    foreach (var newPackageToInstall in newPackagesToInstall)
                    {
                        // we should limit actions to just packages that are in the dependency set of the target we are installing
                        if (allowed.Contains(newPackageToInstall.Id))
                        {
                            // find the package match based on identity
                            var sourceDepInfo = prunedAvailablePackages.SingleOrDefault(p => PackageIdentity.Comparer.Equals(p, newPackageToInstall));

                            if (sourceDepInfo == null)
                            {
                                // this really should never happen
                                throw new InvalidOperationException(string.Format(Strings.PackageNotFound, packageIdentity));
                            }

                            nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(sourceDepInfo, sourceDepInfo.Source, nuGetProject));
                        }
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
                var logger = new ProjectContextLogger(nuGetProjectContext);
                var sourceRepository = await GetSourceRepository(packageIdentity, effectiveSources, logger);
                nuGetProjectActions.Add(NuGetProjectAction.CreateInstallProjectAction(packageIdentity, sourceRepository, nuGetProject));
            }

            // emit resolve actions telemetry event
            TelemetryServiceUtility.EmitEvent(telemetryHelper,
                string.Format(TelemetryConstants.ResolvedActionsStepName, projectId));

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvedActionsToInstallPackage, packageIdentity);
            return nuGetProjectActions;
        }

        /// <summary>
        /// Check all sources in parallel to see if the package exists while respecting the order of the list.
        /// This is only used by PreviewInstall with DependencyBehavior.Ignore.
        /// Since, resolver gather is not used when dependencies are not used,
        /// we simply get the source repository using MetadataResource.Exists
        /// </summary>
        private static async Task<SourceRepository> GetSourceRepository(PackageIdentity packageIdentity,
            IEnumerable<SourceRepository> sourceRepositories,
            Common.ILogger logger)
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
                    var task = Task.Run(() => metadataResource.Exists(packageIdentity, logger, tokenSource.Token), tokenSource.Token);
                    results.Enqueue(new KeyValuePair<SourceRepository, Task<bool>>(sourceRepository, task));
                }
            }

            while (results.Count > 0)
            {
                var pair = results.Dequeue();

                try
                {
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
                catch (Exception ex)
                {
                    logger.LogWarning(
                        string.Format(Strings.Warning_ErrorFindingRepository,
                            pair.Key.PackageSource.Source,
                            ExceptionUtilities.DisplayMessage(ex)));
                }
            }

            if (source == null)
            {
                // no matches were found
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.UnknownPackageSpecificVersion, packageIdentity.Id, packageIdentity.Version));
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
                var action = NuGetProjectAction.CreateUninstallProjectAction(packageReference.PackageIdentity, nuGetProject);
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
            nuGetProjectContext.Log(NuGet.ProjectManagement.MessageLevel.Info, Environment.NewLine);
            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.AttemptingToGatherDependencyInfo, packageIdentity, projectName, packageReferenceTargetFramework);

            // TODO: IncludePrerelease is a big question mark
            var log = new LoggerAdapter(nuGetProjectContext);
            var installedPackageIdentities = (await nuGetProject.GetInstalledPackagesAsync(token)).Select(pr => pr.PackageIdentity);
            var dependencyInfoFromPackagesFolder = await GetDependencyInfoFromPackagesFolder(installedPackageIdentities,
                packageReferenceTargetFramework);

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.ResolvingActionsToUninstallPackage, packageIdentity);
            // Step-2 : Determine if the package can be uninstalled based on the metadata resources
            var packagesToBeUninstalled = UninstallResolver.GetPackagesToBeUninstalled(packageIdentity, dependencyInfoFromPackagesFolder, installedPackageIdentities, uninstallationContext);

            var nuGetProjectActions =
                packagesToBeUninstalled.Select(
                    package => NuGetProjectAction.CreateUninstallProjectAction(package, nuGetProject));

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
                    var packageDependencyInfo = await dependencyInfoResource.ResolvePackage(package, nuGetFramework, Common.NullLogger.Instance, CancellationToken.None);
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
        /// Executes the list of <paramref name="nuGetProjectActions" /> on list of <paramref name="nuGetProjects" /> , which is
        /// likely obtained by calling into
        /// <see
        ///     cref="PreviewInstallPackageAsync(IEnumerable{NuGetProject},string,ResolutionContext,INuGetProjectContext,SourceRepository,IEnumerable{SourceRepository},CancellationToken)" />
        /// <paramref name="nuGetProjectContext" /> is used in the process.
        /// </summary>
        public async Task ExecuteNuGetProjectActionsAsync(IEnumerable<NuGetProject> nuGetProjects,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var projects = nuGetProjects.ToList();

            // find out build integrated projects so that we can arrange them in reverse dependency order
            var buildIntegratedProjectsToUpdate = projects.OfType<BuildIntegratedNuGetProject>().ToList();

            // order won't matter for other type of projects so just add rest of the projects in result
            var sortedProjectsToUpdate = projects.Except(buildIntegratedProjectsToUpdate).ToList();

            if (buildIntegratedProjectsToUpdate.Count > 0)
            {
                var logger = new ProjectContextLogger(nuGetProjectContext);
                var referenceContext = new DependencyGraphCacheContext(logger, Settings);
                _buildIntegratedProjectsUpdateDict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                var projectUniqueNamesForBuildIntToUpdate
                    = buildIntegratedProjectsToUpdate.ToDictionary((project) => project.MSBuildProjectPath);

                // get all build integrated projects of the solution which will be used to map project references
                // of the target projects
                var allBuildIntegratedProjects =
                    (await SolutionManager.GetNuGetProjectsAsync()).OfType<BuildIntegratedNuGetProject>().ToList();

                var dgFile = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(SolutionManager, referenceContext);
                _buildIntegratedProjectsCache = dgFile;
                var allSortedProjects = DependencyGraphSpec.SortPackagesByDependencyOrder(dgFile.Projects);

                var msbuildProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var projectUniqueName in allSortedProjects.Select(e => e.RestoreMetadata.ProjectUniqueName))
                {
                    BuildIntegratedNuGetProject project;
                    if (projectUniqueNamesForBuildIntToUpdate.TryGetValue(projectUniqueName, out project))
                    {
                        sortedProjectsToUpdate.Add(project);
                    }
                }

                // cache these projects which will be used to avoid duplicate restore as part of parent projects
                _buildIntegratedProjectsUpdateDict.AddRange(
                    buildIntegratedProjectsToUpdate.Select(child => new KeyValuePair<string, bool>(child.MSBuildProjectPath, false)));
            }

            // execute all nuget project actions
            foreach (var project in sortedProjectsToUpdate)
            {
                var nugetActions = nuGetProjectActions.Where(action => action.Project.Equals(project));
                await ExecuteNuGetProjectActionsAsync(project, nugetActions, nuGetProjectContext, token);
            }

            // clear cache which could temper with other updates
            _buildIntegratedProjectsUpdateDict?.Clear();
            _buildIntegratedProjectsCache = null;
            _restoreProviderCache = null;
        }

        /// <summary>
        /// Executes the list of <paramref name="nuGetProjectActions" /> on <paramref name="nuGetProject" /> , which is
        /// likely obtained by calling into
        /// <see
        ///     cref="PreviewInstallPackageAsync(NuGetProject,string,ResolutionContext,INuGetProjectContext,SourceRepository,IEnumerable{SourceRepository},CancellationToken)" />
        /// <paramref name="nuGetProjectContext" /> is used in the process.
        /// </summary>
        public async Task ExecuteNuGetProjectActionsAsync(NuGetProject nuGetProject,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(sourceCacheContext);

                await ExecuteNuGetProjectActionsAsync(nuGetProject,
                    nuGetProjectActions,
                    nuGetProjectContext,
                    downloadContext,
                    token);
            }
        }

        /// <summary>
        /// Executes the list of <paramref name="nuGetProjectActions" /> on <paramref name="nuGetProject" /> , which is
        /// likely obtained by calling into
        /// <see
        ///     cref="PreviewInstallPackageAsync(NuGetProject,string,ResolutionContext,INuGetProjectContext,SourceRepository,IEnumerable{SourceRepository},CancellationToken)" />
        /// <paramref name="nuGetProjectContext" /> is used in the process.
        /// </summary>
        public async Task ExecuteNuGetProjectActionsAsync(NuGetProject nuGetProject,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            CancellationToken token)
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

            var projectId = string.Empty;
            nuGetProject.TryGetMetadata<string>(NuGetProjectMetadataKeys.ProjectId, out projectId);

            var stopWatch = Stopwatch.StartNew();

            ExceptionDispatchInfo exceptionInfo = null;

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

                var executedNuGetProjectActions = new Stack<NuGetProjectAction>();
                var packageWithDirectoriesToBeDeleted = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                var ideExecutionContext = nuGetProjectContext.ExecutionContext as IDEExecutionContext;
                if (ideExecutionContext != null)
                {
                    await ideExecutionContext.SaveExpandedNodeStates(SolutionManager);
                }

                var logger = new ProjectContextLogger(nuGetProjectContext);
                Dictionary<PackageIdentity, PackagePreFetcherResult> downloadTasks = null;
                CancellationTokenSource downloadTokenSource = null;

                // batch events argument object
                PackageProjectEventArgs packageProjectEventArgs = null;

                try
                {
                    // PreProcess projects
                    await nuGetProject.PreProcessAsync(nuGetProjectContext, token);

                    var actionsList = nuGetProjectActions.ToList();

                    var hasInstalls = actionsList.Any(action =>
                        action.NuGetProjectActionType == NuGetProjectActionType.Install);

                    if (hasInstalls)
                    {
                        // Make this independently cancelable.
                        downloadTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

                        // Download all packages up front in parallel
                        downloadTasks = await PackagePreFetcher.GetPackagesAsync(
                            actionsList,
                            PackagesFolderNuGetProject,
                            downloadContext,
                            SettingsUtility.GetGlobalPackagesFolder(Settings),
                            logger,
                            downloadTokenSource.Token);

                        // Log download information
                        PackagePreFetcher.LogFetchMessages(
                            downloadTasks.Values,
                            PackagesFolderNuGetProject.Root,
                            logger);
                    }

                    try
                    {
                        if (msbuildProject != null)
                        {
                            //start batch processing for msbuild
                            await msbuildProject.ProjectSystem.BeginProcessingAsync();

                            // raise Nuget batch start event
                            var batchId = Guid.NewGuid().ToString();
                            string name;
                            nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.Name, out name);
                            packageProjectEventArgs = new PackageProjectEventArgs(batchId, name);
                            BatchStart?.Invoke(this, packageProjectEventArgs);
                            PackageProjectEventsProvider.Instance.NotifyBatchStart(packageProjectEventArgs);
                        }

                        foreach (var nuGetProjectAction in actionsList)
                        {
                            executedNuGetProjectActions.Push(nuGetProjectAction);
                            if (nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Uninstall)
                            {
                                await ExecuteUninstallAsync(nuGetProject,
                                    nuGetProjectAction.PackageIdentity,
                                    packageWithDirectoriesToBeDeleted,
                                    nuGetProjectContext, token);
                            }
                            else
                            {
                                // Retrieve the downloaded package
                                // This will wait on the package if it is still downloading
                                var preFetchResult = downloadTasks[nuGetProjectAction.PackageIdentity];
                                using (var downloadPackageResult = await preFetchResult.GetResultAsync())
                                {
                                    // use the version exactly as specified in the nuspec file
                                    var packageIdentity = await downloadPackageResult.PackageReader.GetIdentityAsync(token);

                                    await ExecuteInstallAsync(
                                        nuGetProject,
                                        packageIdentity,
                                        downloadPackageResult,
                                        packageWithDirectoriesToBeDeleted,
                                        nuGetProjectContext,
                                        token);
                                }
                            }

                            if (nuGetProjectAction.NuGetProjectActionType == NuGetProjectActionType.Install)
                            {
                                var identityString = string.Format(CultureInfo.InvariantCulture, "{0} {1}",
                                    nuGetProjectAction.PackageIdentity.Id,
                                    nuGetProjectAction.PackageIdentity.Version.ToNormalizedString());

                                nuGetProjectContext.Log(
                                    ProjectManagement.MessageLevel.Info,
                                    Strings.SuccessfullyInstalled,
                                    identityString,
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
                    }
                    finally
                    {
                        if (msbuildProject != null)
                        {
                            // end batch for msbuild and let it save everything.
                            // always calls it before PostProcessAsync or binding redirects
                            await msbuildProject.ProjectSystem.EndProcessingAsync();
                        }
                    }

                    // Post process
                    await nuGetProject.PostProcessAsync(nuGetProjectContext, token);

                    // Open readme file
                    await OpenReadmeFile(nuGetProject, nuGetProjectContext, token);
                }
                catch (Exception ex)
                {
                    exceptionInfo = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    if (downloadTasks != null)
                    {
                        // Wait for all downloads to cancel and dispose
                        downloadTokenSource.Cancel();

                        foreach (var result in downloadTasks.Values)
                        {
                            await result.EnsureResultAsync();
                            result.Dispose();
                        }

                        downloadTokenSource.Dispose();
                    }

                    if (msbuildProject != null)
                    {
                        // raise nuget batch end event
                        if (packageProjectEventArgs != null)
                        {
                            BatchEnd?.Invoke(this, packageProjectEventArgs);
                            PackageProjectEventsProvider.Instance.NotifyBatchEnd(packageProjectEventArgs);
                        }
                    }
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

                // Save project
                await nuGetProject.SaveAsync(token);

                // Clear direct install
                SetDirectInstall(null, nuGetProjectContext);
            }


            // calculate total time taken to execute all nuget actions
            stopWatch.Stop();
            nuGetProjectContext.Log(
                MessageLevel.Info, Strings.NugetActionsTotalTime,
                DatetimeUtility.ToReadableTimeFormat(stopWatch.Elapsed));

            // emit resolve actions telemetry event
            TelemetryServiceUtility.EmitEvent(nuGetProjectContext.TelemetryService,
                string.Format(TelemetryConstants.ExecuteActionStepName, projectId),
                stopWatch.Elapsed.TotalSeconds);

            if (exceptionInfo != null)
            {
                exceptionInfo.Throw();
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

            var stopWatch = Stopwatch.StartNew();
            var projectId = string.Empty;
            buildIntegratedProject.TryGetMetadata<string>(NuGetProjectMetadataKeys.ProjectId, out projectId);

            // Find all sources used in the project actions
            var sources = new HashSet<SourceRepository>(
                nuGetProjectActions.Where(action => action.SourceRepository != null)
                    .Select(action => action.SourceRepository),
                    new SourceRepositoryComparer());

            // Add all enabled sources for the existing packages
            var enabledSources = SourceRepositoryProvider.GetRepositories();

            sources.UnionWith(enabledSources);

            // Read the current lock file if it exists
            LockFile originalLockFile = null;
            var lockFileFormat = new LockFileFormat();

            var lockFilePath = await buildIntegratedProject.GetAssetsFilePathAsync();

            if (File.Exists(lockFilePath))
            {
                originalLockFile = lockFileFormat.Read(lockFilePath);
            }

            var logger = new ProjectContextLogger(nuGetProjectContext);
            var dependencyGraphContext = new DependencyGraphCacheContext(logger, Settings );

            // Get Package Spec as json object
            var originalPackageSpec = await DependencyGraphRestoreUtility.GetProjectSpec(buildIntegratedProject, dependencyGraphContext);

            // Create a copy to avoid modifying the original spec which may be shared.
            var updatedPackageSpec = originalPackageSpec.Clone();

            var pathContext = NuGetPathContext.Create(Settings);
            var providerCache = new RestoreCommandProvidersCache();

            // For installs only use cache entries newer than the current time.
            // This is needed for scenarios where a new package shows up in search
            // but a previous cache entry does not yet have it.
            // So we want to capture the time once here, then pass it down to the two
            // restores happening in this flow.
            var now = DateTimeOffset.UtcNow;
            Action<SourceCacheContext> cacheModifier = (cache) => cache.MaxAge = now;

            // If the lock file does not exist, restore before starting the operations
            if (originalLockFile == null)
            {
                var originalRestoreResult = await DependencyGraphRestoreUtility.PreviewRestoreAsync(
                    SolutionManager,
                    buildIntegratedProject,
                    originalPackageSpec,
                    dependencyGraphContext,
                    providerCache,
                    cacheModifier,
                    sources,
                    logger,
                    token);

                originalLockFile = originalRestoreResult.Result.LockFile;
            }

            foreach (var action in nuGetProjectActions)
            {
                if (action.NuGetProjectActionType == NuGetProjectActionType.Uninstall)
                {
                    // Remove the package from all frameworks and dependencies section.
                    PackageSpecOperations.RemoveDependency(updatedPackageSpec, action.PackageIdentity.Id);
                }
                else if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                {
                    // This only needs to work this way for project.json
                    // TODO NK [Last] - Does this impact tools? It shouldn't, Verify correctness of this, add tests for Project.Json install and legacy and NET Core package ref install
                    if (updatedPackageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson)
                    {
                        PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, action.PackageIdentity);
                    }
                    else if (updatedPackageSpec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference)
                    {
                        PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, action.PackageIdentity, updatedPackageSpec.TargetFrameworks.Select(e => e.FrameworkName));
                    }
                }
            }

            // Restore based on the modified package spec. This operation does not write the lock file to disk.
            var restoreResult = await DependencyGraphRestoreUtility.PreviewRestoreAsync(
                SolutionManager,
                buildIntegratedProject,
                updatedPackageSpec,
                dependencyGraphContext,
                providerCache,
                cacheModifier,
                sources,
                logger,
                token);

            var nugetProjectActionsList = nuGetProjectActions.ToList();

            var allFrameworks = updatedPackageSpec
                .TargetFrameworks
                .Select(t => t.FrameworkName)
                .Distinct()
                .ToList();

            var unsuccessfulFrameworks = restoreResult
                .Result
                .CompatibilityCheckResults
                .Where(t => !t.Success)
                .Select(t => t.Graph.Framework)
                .Distinct()
                .ToList();

            var successfulFrameworks = allFrameworks
                .Except(unsuccessfulFrameworks)
                .ToList();

            var firstAction = nugetProjectActionsList[0];

            // If the restore failed and this was a single package install, try to install the package to a subset of
            // the target frameworks.
            if (nugetProjectActionsList.Count == 1 &&
                firstAction.NuGetProjectActionType == NuGetProjectActionType.Install &&
                successfulFrameworks.Any() &&
                unsuccessfulFrameworks.Any() &&
                !restoreResult.Result.Success &&
                // Exclude upgrades, for now we take the simplest case.
                !PackageSpecOperations.HasPackage(originalPackageSpec, firstAction.PackageIdentity.Id))
            {
                updatedPackageSpec = originalPackageSpec.Clone();

                PackageSpecOperations.AddOrUpdateDependency(
                    updatedPackageSpec,
                    firstAction.PackageIdentity,
                    successfulFrameworks);

                restoreResult = await DependencyGraphRestoreUtility.PreviewRestoreAsync(
                    SolutionManager,
                    buildIntegratedProject,
                    updatedPackageSpec,
                    dependencyGraphContext,
                    providerCache,
                    cacheModifier,
                    sources,
                    logger,
                    token);
            }

            // If HideWarningsAndErrors is true then restore will not display the warnings and errors.
            // Further, replay errors and warnings only if restore failed because the assets file will not be committed.
            // If there were only warnings then those are written to assets file and committed. The design time build will replay them.
            if (updatedPackageSpec.RestoreSettings.HideWarningsAndErrors &&
                !restoreResult.Result.Success)
            {
                await MSBuildRestoreUtility.ReplayWarningsAndErrorsAsync(restoreResult.Result.LockFile, logger);
            }

            // Build the installation context
            var originalFrameworks = updatedPackageSpec
                .RestoreMetadata
                .OriginalTargetFrameworks
                .GroupBy(x => NuGetFramework.Parse(x))
                .ToDictionary(x => x.Key, x => x.First());
            var installationContext = new BuildIntegratedInstallationContext(
                successfulFrameworks,
                unsuccessfulFrameworks,
                originalFrameworks);

            InstallationCompatibility.EnsurePackageCompatibility(
                buildIntegratedProject,
                pathContext,
                nuGetProjectActions,
                restoreResult.Result);

            // If this build integrated project action represents only uninstalls, mark the entire operation
            // as an uninstall. Otherwise, mark it as an install. This is important because install operations
            // are a bit more sensitive to errors (thus resulting in rollbacks).
            var actionType = NuGetProjectActionType.Install;
            if (nuGetProjectActions.All(x => x.NuGetProjectActionType == NuGetProjectActionType.Uninstall))
            {
                actionType = NuGetProjectActionType.Uninstall;
            }

            stopWatch.Stop();
            TelemetryServiceUtility.EmitEvent(nuGetProjectContext.TelemetryService,
                string.Format(TelemetryConstants.PreviewBuildIntegratedStepName, projectId),
                stopWatch.Elapsed.TotalSeconds);

            return new BuildIntegratedProjectAction(
                buildIntegratedProject,
                nuGetProjectActions.First().PackageIdentity,
                actionType,
                originalLockFile,
                restoreResult,
                sources.ToList(),
                nugetProjectActionsList,
                installationContext);
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
                // Get all install actions
                var ignoreActions = new HashSet<NuGetProjectAction>();
                var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var action in projectAction.OriginalActions.Reverse())
                {
                    if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        installedIds.Add(action.PackageIdentity.Id);
                    }
                    else if (installedIds.Contains(action.PackageIdentity.Id))
                    {
                        ignoreActions.Add(action);
                    }
                }

                foreach (var originalAction in projectAction.OriginalActions.Where(e => !ignoreActions.Contains(e)))
                {
                    if (originalAction.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        // Install the package to the project
                        await buildIntegratedProject.InstallPackageAsync(
                            originalAction.PackageIdentity.Id,
                            new VersionRange(originalAction.PackageIdentity.Version),
                            nuGetProjectContext,
                            projectAction.InstallationContext,
                            token: token);
                    }
                    else if (originalAction.NuGetProjectActionType == NuGetProjectActionType.Uninstall)
                    {
                        await buildIntegratedProject.UninstallPackageAsync(
                            originalAction.PackageIdentity,
                            nuGetProjectContext: nuGetProjectContext,
                            token: token);
                    }
                }

                var logger = new ProjectContextLogger(nuGetProjectContext);
                var referenceContext = new DependencyGraphCacheContext(logger, Settings);
                var pathContext = NuGetPathContext.Create(Settings);
                var pathResolver = new FallbackPackagePathResolver(pathContext);

                var now = DateTime.UtcNow;
                Action<SourceCacheContext> cacheContextModifier = c => c.MaxAge = now;

                // Check if current project is there in update cache and needs revaluation
                var isProjectUpdated = false;
                if (_buildIntegratedProjectsUpdateDict != null &&
                    _buildIntegratedProjectsUpdateDict.TryGetValue(
                        buildIntegratedProject.MSBuildProjectPath,
                        out isProjectUpdated) &&
                    isProjectUpdated)
                {
                    await DependencyGraphRestoreUtility.RestoreProjectAsync(
                            SolutionManager,
                            buildIntegratedProject,
                            referenceContext,
                            GetRestoreProviderCache(),
                            cacheContextModifier,
                            projectAction.Sources,
                            logger,
                            token);
                }
                else
                {
                    // Write out the lock file
                    await RestoreRunner.CommitAsync(projectAction.RestoreResultPair, token);
                }

                // Write out a message for each action
                foreach (var action in actions)
                {
                    var identityString = string.Format(CultureInfo.InvariantCulture, "{0} {1}",
                        action.PackageIdentity.Id,
                        action.PackageIdentity.Version.ToNormalizedString());

                    if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        nuGetProjectContext.Log(
                            MessageLevel.Info,
                            Strings.SuccessfullyInstalled,
                            identityString,
                            buildIntegratedProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                    }
                    else
                    {
                        // uninstall
                        nuGetProjectContext.Log(
                            MessageLevel.Info,
                            Strings.SuccessfullyUninstalled,
                            identityString,
                            buildIntegratedProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
                    }
                }

                // Run init.ps1 scripts
                var addedPackages = BuildIntegratedRestoreUtility.GetAddedPackages(
                    projectAction.OriginalLockFile,
                    restoreResult.LockFile);
                await BuildIntegratedRestoreUtility.ExecuteInitPs1ScriptsAsync(
                    buildIntegratedProject,
                    addedPackages,
                    pathResolver,
                    nuGetProjectContext,
                    token);

                // find list of buildintegrated projects
                var projects = (await SolutionManager.GetNuGetProjectsAsync()).OfType<BuildIntegratedNuGetProject>().ToList();

                // build reference cache if not done already
                if (_buildIntegratedProjectsCache == null)
                {
                    _buildIntegratedProjectsCache = await
                        DependencyGraphRestoreUtility.GetSolutionRestoreSpec(SolutionManager, referenceContext);
                }

                // Restore parent projects. These will be updated to include the transitive changes.
                var parents = BuildIntegratedRestoreUtility.GetParentProjectsInClosure(
                    projects,
                    buildIntegratedProject,
                    _buildIntegratedProjectsCache);
                // The settings contained in the context are applied to the dg spec.
                var dgSpecForParents = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(SolutionManager, referenceContext);
                dgSpecForParents = dgSpecForParents.WithoutRestores();

                foreach (var parent in parents)
                {
                    // if this parent exists in update cache, then update it's entry to be re-evaluated
                    if (_buildIntegratedProjectsUpdateDict != null &&
                        _buildIntegratedProjectsUpdateDict.ContainsKey(parent.MSBuildProjectPath))
                    {
                        _buildIntegratedProjectsUpdateDict[parent.MSBuildProjectPath] = true;
                    }
                    else
                    {
                        // Mark project for restore
                        dgSpecForParents.AddRestore(parent.MSBuildProjectPath);
                    }
                }

                if (dgSpecForParents.Restore.Count > 0)
                {
                    // Restore and commit the lock file to disk regardless of the result
                    // This will restore all parents in a single restore 
                    await DependencyGraphRestoreUtility.RestoreAsync(
                        dgSpecForParents,
                        referenceContext,
                        GetRestoreProviderCache(),
                        cacheContextModifier,
                        projectAction.Sources,
                        false, // No need to force restore as the inputs would've changed here anyways
                        logger,
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

            await OpenReadmeFile(buildIntegratedProject, nuGetProjectContext, token);
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
                            using (var downloadResourceResult = new DownloadResourceResult(File.OpenRead(packagePath), nuGetProjectAction.SourceRepository?.PackageSource?.Source))
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

        private Task OpenReadmeFile(NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var executionContext = nuGetProjectContext.ExecutionContext;
            if (executionContext != null
                && executionContext.DirectInstall != null)
            {
                //packagesPath is different for project.json vs Packages.config scenarios. So check if the project is a build-integrated project
                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
                var readmeFilePath = string.Empty;

                if (buildIntegratedProject != null)
                {
                    var pathContext = NuGetPathContext.Create(Settings);
                    var pathResolver = new FallbackPackagePathResolver(pathContext);
                    var identity = nuGetProjectContext.ExecutionContext.DirectInstall;
                    var packageFolderPath = pathResolver.GetPackageDirectory(identity.Id, identity.Version);

                    if (!string.IsNullOrEmpty(packageFolderPath))
                    {
                        readmeFilePath = Path.Combine(packageFolderPath, Constants.ReadmeFileName);
                    }
                }
                else
                {
                    var packagePath = PackagesFolderNuGetProject.GetInstalledPackageFilePath(executionContext.DirectInstall);

                    if (!string.IsNullOrEmpty(packagePath))
                    {
                        readmeFilePath = Path.Combine(Path.GetDirectoryName(packagePath), Constants.ReadmeFileName);
                    }
                }

                if (!token.IsCancellationRequested &&
                    !string.IsNullOrEmpty(readmeFilePath) &&
                    File.Exists(readmeFilePath))
                {
                    return executionContext.OpenFile(readmeFilePath);
                }
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// RestorePackage is only allowed on a folderNuGetProject. In most cases, one will simply use the
        /// packagesFolderPath from NuGetPackageManager
        /// to create a folderNuGetProject before calling into this method
        /// </summary>
        public async Task<bool> RestorePackageAsync(
            PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            PackageDownloadContext downloadContext,
            IEnumerable<SourceRepository> sourceRepositories,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (PackageExistsInPackagesFolder(packageIdentity, nuGetProjectContext.PackageExtractionContext.PackageSaveMode))
            {
                return false;
            }

            token.ThrowIfCancellationRequested();
            nuGetProjectContext.Log(MessageLevel.Info, string.Format(Strings.RestoringPackage, packageIdentity));
            var enabledSources = (sourceRepositories != null && sourceRepositories.Any()) ? sourceRepositories :
                SourceRepositoryProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);

            token.ThrowIfCancellationRequested();

            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                enabledSources,
                packageIdentity,
                downloadContext,
                SettingsUtility.GetGlobalPackagesFolder(Settings),
                new ProjectContextLogger(nuGetProjectContext),
                token))
            {
                // Install package whether returned from the cache or a direct download
                await PackagesFolderNuGetProject.InstallPackageAsync(packageIdentity, downloadResult, nuGetProjectContext, token);
            }

            return true;
        }

        public Task<bool> CopySatelliteFilesAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            return PackagesFolderNuGetProject.CopySatelliteFilesAsync(packageIdentity, nuGetProjectContext, token);
        }

        /// <summary>
        /// Checks whether package exists in packages folder and verifies that nupkg and nuspec are present as specified by packageSaveMode
        /// </summary>
        public bool PackageExistsInPackagesFolder(PackageIdentity packageIdentity, PackageSaveMode packageSaveMode)
        {
            return PackagesFolderNuGetProject.PackageExists(packageIdentity, packageSaveMode);
        }

        public bool PackageExistsInPackagesFolder(PackageIdentity packageIdentity)
        {
            return PackagesFolderNuGetProject.PackageExists(packageIdentity);
        }

        private async Task ExecuteInstallAsync(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            DownloadResourceResult resourceResult,
            HashSet<PackageIdentity> packageWithDirectoriesToBeDeleted,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            // TODO: EnsurePackageCompatibility check should be performed in preview. Can easily avoid a lot of rollback
            await InstallationCompatibility.EnsurePackageCompatibilityAsync(nuGetProject, packageIdentity, resourceResult, token);

            packageWithDirectoriesToBeDeleted.Remove(packageIdentity);

            await nuGetProject.InstallPackageAsync(packageIdentity, resourceResult, nuGetProjectContext, token);
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
            foreach (var otherNuGetProject in (await solutionManager.GetNuGetProjectsAsync()))
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
                nuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, Strings.PackageDoesNotExistInFolder, packageIdentity, PackagesFolderNuGetProject.Root);
                return false;
            }

            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.RemovingPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            // 2. Delete the package folder and files from the root directory of this FileSystemNuGetProject
            // Remember that the following code may throw System.UnauthorizedAccessException
            await PackagesFolderNuGetProject.DeletePackage(packageIdentity, nuGetProjectContext, token);
            nuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.RemovedPackageFromFolder, packageIdentity, PackagesFolderNuGetProject.Root);
            return true;
        }

        public static Task<ResolvedPackage> GetLatestVersionAsync(
            string packageId,
            NuGetFramework framework,
            ResolutionContext resolutionContext,
            SourceRepository primarySourceRepository,
            Common.ILogger log,
            CancellationToken token)
        {
            return GetLatestVersionAsync(
                packageId,
                framework,
                resolutionContext,
                new List<SourceRepository> { primarySourceRepository },
                log,
                token);
        }

        public static Task<ResolvedPackage> GetLatestVersionAsync(
            string packageId,
            NuGetProject project,
            ResolutionContext resolutionContext,
            SourceRepository primarySourceRepository,
            Common.ILogger log,
            CancellationToken token)
        {
            NuGetFramework framework;
            if (!project.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out framework))
            {
                // Default to the any framework if the project does not specify a framework.
                framework = NuGetFramework.AnyFramework;
            }

            return GetLatestVersionAsync(
                packageId,
                framework,
                resolutionContext,
                new List<SourceRepository> { primarySourceRepository },
                log,
                token);
        }

        public static async Task<ResolvedPackage> GetLatestVersionAsync(
            string packageId,
            NuGetProject project,
            ResolutionContext resolutionContext,
            IEnumerable<SourceRepository> sources,
            Common.ILogger log,
            CancellationToken token)
        {
            var tasks = new List<Task<NuGetVersion>>();

            NuGetFramework framework;
            if (!project.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out framework))
            {
                // Default to the any framework if the project does not specify a framework.
                framework = NuGetFramework.AnyFramework;
            }

            return await GetLatestVersionAsync(packageId, framework, resolutionContext, sources, log, token);
        }

        public static async Task<ResolvedPackage> GetLatestVersionAsync(
            string packageId,
            NuGetFramework framework,
            ResolutionContext resolutionContext,
            IEnumerable<SourceRepository> sources,
            Common.ILogger log,
            CancellationToken token)
        {
            var tasks = new List<Task<ResolvedPackage>>();

            foreach (var source in sources)
            {
                tasks.Add(Task.Run(async ()
                    => await GetLatestVersionCoreAsync(packageId, framework, resolutionContext, source, log, token)));
            }

            var resolvedPackages = await Task.WhenAll(tasks);
            var latestVersion = resolvedPackages.Select(v => v.LatestVersion).Where(v => v != null).Max();

            return new ResolvedPackage(latestVersion, resolvedPackages.Any(p => p.Exists));
        }

        private static async Task<ResolvedPackage> GetLatestVersionCoreAsync(
            string packageId,
            NuGetFramework framework,
            ResolutionContext resolutionContext,
            SourceRepository source,
            Common.ILogger log,
            CancellationToken token)
        {
            var dependencyInfoResource = await source.GetResourceAsync<DependencyInfoResource>();

            // Resolve the package for the project framework and cache the results in the
            // resolution context for the gather to use during the next step.
            // Using the metadata resource will result in multiple calls to the same url during an install.
            var packages = (await dependencyInfoResource.ResolvePackages(packageId, framework, log, token)).ToList();

            Debug.Assert(resolutionContext.GatherCache != null);

            // Cache the results, even if the package was not found.
            resolutionContext.GatherCache.AddAllPackagesForId(
                source.PackageSource,
                packageId,
                framework,
                packages);

            // Find the latest version
            var latestVersion = packages.Where(package => (package.Listed || resolutionContext.IncludeUnlisted)
                && (resolutionContext.IncludePrerelease || !package.Version.IsPrerelease))
                .OrderByDescending(package => package.Version, VersionComparer.Default)
                .Select(package => package.Version)
                .FirstOrDefault();

            return new ResolvedPackage(latestVersion, packages.Count > 0);
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

        private RestoreCommandProvidersCache GetRestoreProviderCache()
        {
            if (_restoreProviderCache == null)
            {
                _restoreProviderCache = new RestoreCommandProvidersCache();
            }

            return _restoreProviderCache;
        }
    }
}