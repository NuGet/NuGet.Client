// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    internal class PackageSolutionDetailControlModel : DetailControlModel
    {
        private ISolutionManager _solutionManager;
        private IEnumerable<IVsPackageManagerProvider> _packageManagerProviders;
        private string _installedVersions; // The text describing the installed versions information, such as "not installed", "multiple versions installed" etc.
        private int _installedVersionsCount; // the count of different installed versions
        private bool _canUninstall;
        private bool _canInstall;
        // Indicates whether the SelectCheckBoxState is being updated in code. True means the state is being updated by code, while false means the state is changed by user clicking the checkbox.
        private bool _updatingSelectCheckBoxState;
        private bool? _selectCheckBoxState;
        private List<PackageInstallationInfo> _projects; // List of projects in the solution

        private PackageSolutionDetailControlModel(IEnumerable<ProjectContextInfo> projects)
            : base(projects)
        {
        }

        private async ValueTask<PackageSolutionDetailControlModel> InitializeAsync(
            ISolutionManager solutionManager,
            IEnumerable<ProjectContextInfo> projects,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            CancellationToken cancellationToken)
        {
            _solutionManager = solutionManager;
            _solutionManager.NuGetProjectAdded += SolutionProjectChanged;
            _solutionManager.NuGetProjectRemoved += SolutionProjectChanged;
            _solutionManager.NuGetProjectUpdated += SolutionProjectChanged;
            _solutionManager.NuGetProjectRenamed += SolutionProjectChanged;

            // when the SelectedVersion is changed, we need to update CanInstall and CanUninstall.
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedVersion))
                {
                    UpdateCanInstallAndCanUninstall();
                }
            };

            _packageManagerProviders = packageManagerProviders;

            await CreateProjectListsAsync(cancellationToken);

            return this;
        }

        public static ValueTask<PackageSolutionDetailControlModel> CreateAsync(
            ISolutionManager solutionManager,
            IEnumerable<ProjectContextInfo> projects,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            CancellationToken cancellationToken)
        {
            var packageSolutionDetailControlModel = new PackageSolutionDetailControlModel(projects);
            return packageSolutionDetailControlModel.InitializeAsync(solutionManager, projects, packageManagerProviders, cancellationToken);
        }

        public List<PackageInstallationInfo> Projects
        {
            get => _projects;
            set
            {
                _projects = value;
                OnPropertyChanged(nameof(Projects));
            }
        }

        public override bool IsSolution => true;

        public override async Task RefreshAsync(CancellationToken cancellationToken)
        {
            await UpdateInstalledVersionsAsync(cancellationToken);
        }

        public string InstalledVersions
        {
            get => _installedVersions;
            set
            {
                _installedVersions = value;
                OnPropertyChanged(nameof(InstalledVersions));
            }
        }

        private async Task UpdateInstalledVersionsAsync(CancellationToken cancellationToken)
        {
            var hash = new HashSet<NuGetVersion>();

            foreach (var project in _projects)
            {
                try
                {
                    var installedVersion = await GetInstalledPackageAsync(project.NuGetProject, Id, cancellationToken);
                    if (installedVersion != null)
                    {
                        project.InstalledVersion = installedVersion.PackageIdentity.Version;
                        hash.Add(installedVersion.PackageIdentity.Version);
                        project.AutoReferenced = (installedVersion as BuildIntegratedPackageReference)?.Dependency?.AutoReferenced == true;
                    }
                    else
                    {
                        project.InstalledVersion = null;
                        project.AutoReferenced = false;
                    }
                }
                catch (Exception ex)
                {
                    project.InstalledVersion = null;

                    // we don't expect it to throw any exception here. But in some edge case when opening manager ui at solution is the
                    // first NuGet operation, and packages.config file is not valid for any of the project, then it will throw here which
                    // should be ignored since we already show a error bar on manager ui to show this exact error.
                    ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
                }
            }

            InstalledVersionsCount = hash.Count;

            if (hash.Count == 0)
            {
                InstalledVersions = Resources.Text_NotInstalled;
            }
            else if (hash.Count == 1)
            {
                var displayVersion = new DisplayVersion(
                    hash.First(),
                    string.Empty);
                InstalledVersions = displayVersion.ToString();
            }
            else
            {
                InstalledVersions = Resources.Text_MultipleVersionsInstalled;
            }

            UpdateCanInstallAndCanUninstall();
            AutoSelectProjects();
        }

        /// <summary>
        /// This method is called from several methods that are called from properties and LINQ queries
        /// It is likely not called more than once in an action. So, consolidating the use of JTF.Run in this method
        /// </summary>
        private static async Task<Packaging.PackageReference> GetInstalledPackageAsync(ProjectContextInfo project, string id, CancellationToken cancellationToken)
        {
            var installedPackages = await project.GetInstalledPackagesAsync(cancellationToken);
            var installedPackage = installedPackages
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, id))
                .FirstOrDefault();
            return installedPackage;
        }

        protected override async Task CreateVersionsAsync(CancellationToken cancellationToken)
        {
            _versions = new List<DisplayVersion>();
            var allVersions = _allPackageVersions?.Where(v => v.version != null).OrderByDescending(v => v);

            // allVersions is null if server doesn't return any versions.
            if (allVersions == null || !allVersions.Any())
            {
                return;
            }

            // null, if no version constraint defined in package.config
            var allowedVersions = await GetAllowedVersionsAsync(cancellationToken);
            var allVersionsAllowed = allVersions.Where(v => allowedVersions.Satisfies(v.version)).ToArray();

            // null, if all versions are allowed to install or update
            var blockedVersions = allVersions
                .Select(v => v.version)
                .Where(v => !allVersionsAllowed.Any(allowed => allowed.version.Equals(v)))
                .ToArray();

            // get latest prerelease or stable based on allowed versions
            var latestPrerelease = allVersionsAllowed.FirstOrDefault(v => v.version.IsPrerelease);
            var latestStableVersion = allVersionsAllowed.FirstOrDefault(v => !v.version.IsPrerelease);

            if (latestPrerelease.version != null
                && (latestStableVersion.version == null || latestPrerelease.version > latestStableVersion.version))
            {
                _versions.Add(new DisplayVersion(latestPrerelease.version, Resources.Version_LatestPrerelease, isDeprecated: latestPrerelease.isDeprecated));
            }

            if (latestStableVersion.version != null)
            {
                _versions.Add(new DisplayVersion(latestStableVersion.version, Resources.Version_LatestStable, isDeprecated: latestStableVersion.isDeprecated));
            }

            // add a separator
            if (_versions.Count > 0)
            {
                _versions.Add(null);
            }

            // first add all the available versions to be updated
            foreach (var version in allVersionsAllowed)
            {
                _versions.Add(new DisplayVersion(version.version, string.Empty, isDeprecated: version.isDeprecated));
            }

            var selectedProjects = (await GetConstraintsForSelectedProjectsAsync(cancellationToken)).ToArray();

            var autoReferenced = selectedProjects.Length > 0 && selectedProjects.All(e => e.IsAutoReferenced);

            // Disable controls if this is an auto referenced package.
            SetAutoReferencedCheck(autoReferenced);

            // Add disabled versions
            AddBlockedVersions(blockedVersions);

            SelectVersion();

            OnPropertyChanged(nameof(Versions));
        }

        public int InstalledVersionsCount
        {
            get => _installedVersionsCount;
            set
            {
                _installedVersionsCount = value;
                OnPropertyChanged(nameof(InstalledVersionsCount));
            }
        }

        // The event handler that is called when a project is added, removed or renamed.
        private void SolutionProjectChanged(object sender, NuGetProjectEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => CreateProjectListsAsync(CancellationToken.None))
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(SolutionProjectChanged)));
        }

        protected override void DependencyBehavior_SelectedChanged(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => CreateVersionsAndUpdateInstallUninstallAsync())
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(Project_SelectedChanged)));
        }

        public override void CleanUp()
        {
            // unhook event handlers
            _solutionManager.NuGetProjectAdded -= SolutionProjectChanged;
            _solutionManager.NuGetProjectRemoved -= SolutionProjectChanged;
            _solutionManager.NuGetProjectRenamed -= SolutionProjectChanged;
            _solutionManager.NuGetProjectUpdated -= SolutionProjectChanged;

            Options.SelectedChanged -= DependencyBehavior_SelectedChanged;

            if (Projects != null)
            {
                foreach (var project in Projects)
                {
                    project.SelectedChanged -= Project_SelectedChanged;
                }
            }
        }

        // Creates the project lists. Also called after a project is added/removed/renamed.
        private async Task CreateProjectListsAsync(CancellationToken cancellationToken)
        {
            // unhook event handler
            if (Projects != null)
            {
                foreach (var project in Projects)
                {
                    project.SelectedChanged -= Project_SelectedChanged;
                }
            }

            var listOfProjectContexts = new List<ProjectContextInfo>();
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService))
            {
                Assumes.NotNull(nugetProjectManagerService);
                // This is pretty inefficient, going to the server to get the list of Guids then creating each contextinfo which also goes back to the server
                var projectIds = await nugetProjectManagerService.GetProjectsAsync(cancellationToken);

                foreach (var projectId in projectIds)
                {
                    if (Guid.TryParse(projectId, out Guid projectGuid))
                    {
                        var projectContext = await ProjectContextInfo.CreateAsync(projectGuid, cancellationToken);
                        listOfProjectContexts.Add(projectContext);
                    }
                }
            }

            Projects = listOfProjectContexts.Select(
                projectContextInfo => new PackageInstallationInfo(projectContextInfo))
                .ToList();

            // hook up event handler
            foreach (var project in Projects)
            {
                project.SelectedChanged += Project_SelectedChanged;
            }

            await UpdateInstalledVersionsAsync(cancellationToken);
            UpdateSelectCheckBoxState();
            CanUninstall = false;
            CanInstall = false;
        }

        public bool? SelectCheckBoxState
        {
            get
            {
                return _selectCheckBoxState;
            }
            set
            {
                _selectCheckBoxState = value;
                OnPropertyChanged(nameof(SelectCheckBoxState));
            }
        }

        public bool CanInstall
        {
            get
            {
                return _canInstall;
            }
            set
            {
                _canInstall = value;
                OnPropertyChanged(nameof(CanInstall));
            }
        }

        public bool CanUninstall
        {
            get
            {
                return _canUninstall;
            }
            set
            {
                _canUninstall = value;
                OnPropertyChanged(nameof(CanUninstall));
            }
        }

        private void Project_SelectedChanged(object sender, EventArgs e)
        {
            UpdateSelectCheckBoxState();

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => CreateVersionsAndUpdateInstallUninstallAsync())
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(Project_SelectedChanged)));
        }

        private async Task CreateVersionsAndUpdateInstallUninstallAsync()
        {
            // update versions list everytime selected projects change
            await CreateVersionsAsync(CancellationToken.None);

            UpdateCanInstallAndCanUninstall();
        }

        private void UpdateSelectCheckBoxState()
        {
            _updatingSelectCheckBoxState = true;
            int countSelected = Projects.Count(project => project.IsSelected);
            if (countSelected == 0)
            {
                SelectCheckBoxState = false;
            }
            else if (countSelected == Projects.Count)
            {
                SelectCheckBoxState = true;
            }
            else
            {
                SelectCheckBoxState = null;
            }

            _updatingSelectCheckBoxState = false;
        }

        private void UpdateCanInstallAndCanUninstall()
        {
            CanUninstall = Projects.Any(project => project.IsSelected && project.InstalledVersion != null && !project.AutoReferenced);

            CanInstall = SelectedVersion != null && Projects.Any(
                project => project.IsSelected &&
                    VersionComparer.Default.Compare(SelectedVersion.Version, project.InstalledVersion) != 0);
        }

        private async ValueTask<IEnumerable<ProjectVersionConstraint>> GetConstraintsForSelectedProjectsAsync(CancellationToken cancellationToken)
        {
            var selectedProjectsNames = new List<string>();
            foreach (var project in Projects.Where(p => p.IsSelected).ToList())
            {
                string projectName = await project.NuGetProject.GetMetadataAsync<string>(NuGetProjectMetadataKeys.Name, cancellationToken);
                selectedProjectsNames.Add(projectName);
            }

            return _projectVersionConstraints.Where(e => selectedProjectsNames.Contains(e.ProjectName, StringComparer.OrdinalIgnoreCase));
        }

        private async ValueTask<VersionRange> GetAllowedVersionsAsync(CancellationToken cancellationToken)
        {
            // allowed version ranges for selected list of projects
            var allowedVersionsRange = (await GetConstraintsForSelectedProjectsAsync(cancellationToken))
                .Select(e => e.VersionRange)
                .Where(v => v != null);

            // if version constraints exist then merge all the ranges and return common range which satisfies all
            return allowedVersionsRange.Any() ? VersionRange.CommonSubSet(allowedVersionsRange) : VersionRange.All;
        }

        internal async Task SelectAllProjectsAsync(bool select, CancellationToken cancellationToken)
        {
            if (_updatingSelectCheckBoxState)
            {
                return;
            }

            foreach (var project in Projects)
            {
                project.IsSelected = select;
            }
            await CreateVersionsAndUpdateInstallUninstallAsync();
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100", Justification = "NuGet/Home#4833 Baseline")]
        protected override async void OnCurrentPackageChanged()
        {
            if (_searchResultPackage == null)
            {
                return;
            }

            await UpdateInstalledVersionsAsync(CancellationToken.None);

            // update alternative package managers
            if (_packageManagerProviders.Any())
            {
                // clear providers first
                foreach (var p in _projects)
                {
                    p.Providers = null;
                }

                // update the providers list async
                foreach (var p in _projects)
                {
                    p.Providers = await AlternativePackageManagerProviders.CalculateAlternativePackageManagersAsync(_packageManagerProviders, Id, p.ProjectName);
                }
            }
        }

        // auto select projects based on the current tab and currently selected package
        private void AutoSelectProjects()
        {
            if (_filter == ItemFilter.Consolidate ||
                _filter == ItemFilter.UpdatesAvailable)
            {
                foreach (var project in _projects)
                {
                    project.IsSelected = project.InstalledVersion != null;
                }
            }
        }

        public override void OnFilterChanged(ItemFilter? previousFilter, ItemFilter currentFilter)
        {
            base.OnFilterChanged(previousFilter, currentFilter);

            // clear selection if filter is changed from Consolidate/UpdateAvailable
            // to Browse/Install.
            if ((previousFilter == ItemFilter.Consolidate || previousFilter == ItemFilter.UpdatesAvailable) &&
                (_filter == ItemFilter.All || _filter == ItemFilter.Installed))
            {
                foreach (var project in _projects)
                {
                    project.IsSelected = false;
                }
            }
        }

        public override IEnumerable<ProjectContextInfo> GetSelectedProjects(UserAction action)
        {
            var selectedProjects = new List<ProjectContextInfo>();

            foreach (var project in _projects)
            {
                if (project.IsSelected == false)
                {
                    continue;
                }

                if (action.Action == NuGetProjectActionType.Install)
                {
                    // for install, the installed version can't be the same as the version to be installed.
                    // AutoReferenced packages should be ignored
                    if (!project.AutoReferenced &&
                        VersionComparer.Default.Compare(
                        project.InstalledVersion,
                        action.Version) != 0)
                    {
                        selectedProjects.Add(project.NuGetProject);
                    }
                }
                else
                {
                    // for uninstall, the package must be already installed
                    if (project.InstalledVersion != null && !project.AutoReferenced)
                    {
                        selectedProjects.Add(project.NuGetProject);
                    }
                }
            }

            return selectedProjects;
        }
    }
}
