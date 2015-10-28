// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class PackageSolutionDetailControlModel : DetailControlModel
    {
        private readonly ISolutionManager _solutionManager;

        private IEnumerable<IVsPackageManagerProvider> _packageManagerProviders;

        // List of projects in the solution
        private List<PackageInstallationInfo> _projects;

        public List<PackageInstallationInfo> Projects
        {
            get
            {
                return _projects;
            }
            set
            {
                _projects = value;
                OnPropertyChanged(nameof(Projects));
            }
        }

        public override bool IsSolution
        {
            get { return true; }
        }

        public override void Refresh()
        {
            UpdateInstalledVersions();
        }

        // The text describing the installed versions information, such as "not installed",
        // "multiple versions installed" etc.
        private string _installedVersions;

        public string InstalledVersions
        {
            get
            {
                return _installedVersions;
            }
            set
            {
                _installedVersions = value;
                OnPropertyChanged(nameof(InstalledVersions));
            }
        }

        private void UpdateInstalledVersions()
        {
            var hash = new HashSet<NuGetVersion>();

            foreach (var project in _projects)
            {
                var installedVersion = GetInstalledPackage(project.NuGetProject, Id);
                if (installedVersion != null)
                {
                    project.InstalledVersion = installedVersion.PackageIdentity.Version;
                    hash.Add(installedVersion.PackageIdentity.Version);
                }
                else
                {
                    project.InstalledVersion = null;
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
        }

        private NuGetVersion GetInstalledVersion(NuGetProject project, string packageId)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var installedPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                var installedPackage = installedPackages
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId))
                    .FirstOrDefault();

                if (installedPackage != null)
                {
                    return installedPackage.PackageIdentity.Version;
                }
                else
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// This method is called from several methods that are called from properties and LINQ queries
        /// It is likely not called more than once in an action. So, consolidating the use of JTF.Run in this method
        /// </summary>
        private static Packaging.PackageReference GetInstalledPackage(NuGetProject project, string id)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    var installedPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                    var installedPackage = installedPackages
                        .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, id))
                        .FirstOrDefault();
                    return installedPackage;
                });
        }

        protected override void CreateVersions()
        {
            _versions = new List<DisplayVersion>();
            var allVersions = _allPackageVersions.OrderByDescending(v => v);
            var latestPrerelease = allVersions.FirstOrDefault(v => v.IsPrerelease);
            var latestStableVersion = allVersions.FirstOrDefault(v => !v.IsPrerelease);

            if (latestPrerelease != null
                && (latestStableVersion == null || latestPrerelease > latestStableVersion))
            {
                _versions.Add(new DisplayVersion(latestPrerelease, Resources.Version_LatestPrerelease));
            }

            if (latestStableVersion != null)
            {
                _versions.Add(new DisplayVersion(latestStableVersion, Resources.Version_LatestStable));
            }

            // add a separator
            if (_versions.Count > 0)
            {
                _versions.Add(null);
            }

            foreach (var version in allVersions)
            {
                _versions.Add(new DisplayVersion(version, string.Empty));
            }

            SelectVersion();
            OnPropertyChanged("Versions");
        }

        // the count of different installed versions
        private int _installedVersionsCount;

        public int InstalledVersionsCount
        {
            get
            {
                return _installedVersionsCount;
            }
            set
            {
                _installedVersionsCount = value;
                OnPropertyChanged(nameof(InstalledVersionsCount));
            }
        }

        public PackageSolutionDetailControlModel(
            ISolutionManager solutionManager,
            IEnumerable<NuGetProject> projects,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
            :
                base(projects)
        {
            _solutionManager = solutionManager;
            _solutionManager.NuGetProjectAdded += SolutionProjectChanged;
            _solutionManager.NuGetProjectRemoved += SolutionProjectChanged;
            _solutionManager.NuGetProjectRenamed += SolutionProjectChanged;

            // when the SelectedVersion is changed, we need to update CanInstall
            // and CanUninstall.
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedVersion))
                {
                    UpdateCanInstallAndCanUninstall();
                }
            };

            _packageManagerProviders = packageManagerProviders;

            CreateProjectLists();
        }

        // The event handler that is called when a project is added, removed or renamed.
        private void SolutionProjectChanged(object sender, NuGetProjectEventArgs e)
        {
            CreateProjectLists();
        }

        public override void CleanUp()
        {
            // unhook event handlers
            _solutionManager.NuGetProjectAdded -= SolutionProjectChanged;
            _solutionManager.NuGetProjectRemoved -= SolutionProjectChanged;
            _solutionManager.NuGetProjectRenamed -= SolutionProjectChanged;

            if (Projects != null)
            {
                foreach (var project in Projects)
                {
                    project.SelectedChanged -= Project_SelectedChanged;
                }
            }
        }

        // Creates the project lists. Also called after a project is added/removed/renamed.
        private void CreateProjectLists()
        {
            // unhook event handler
            if (Projects != null)
            {
                foreach (var project in Projects)
                {
                    project.SelectedChanged -= Project_SelectedChanged;
                }
            }

            _nugetProjects = _solutionManager.GetNuGetProjects();
            Projects = _nugetProjects.Select(
                nugetProject => new PackageInstallationInfo(nugetProject))
                .ToList();

            // hook up event handler
            foreach (var project in Projects)
            {
                project.SelectedChanged += Project_SelectedChanged;
            }

            UpdateInstalledVersions();
            UpdateSelectCheckBoxState();
            CanUninstall = false;
            CanInstall = false;
        }

        // Indicates whether the SelectCheckBoxState is being updated in code. True means the state is
        // being updated by code, while false means the state is changed by user clicking the checkbox.
        private bool _updatingSelectCheckBoxState;

        // the state the select checkbox
        private bool? _selectCheckBoxState;

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

        private bool _canInstall;

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

        private bool _canUninstall;

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
            CanUninstall = Projects.Any(project => project.IsSelected && project.InstalledVersion != null);

            CanInstall = Projects.Any(
                project => project.IsSelected &&
                    VersionComparer.Default.Compare(SelectedVersion.Version, project.InstalledVersion) != 0);
        }

        public void SelectAllProjects()
        {
            if (_updatingSelectCheckBoxState)
            {
                return;
            }

            foreach (var project in Projects)
            {
                project.IsSelected = true;
            }

            UpdateCanInstallAndCanUninstall();
        }

        public void UnselectAllProjects()
        {
            if (_updatingSelectCheckBoxState)
            {
                return;
            }

            foreach (var project in Projects)
            {
                project.IsSelected = false;
            }

            UpdateCanInstallAndCanUninstall();
        }

        private static bool IsInstalled(NuGetProject project, string id)
        {
            var packageReference = GetInstalledPackage(project, id);
            return packageReference != null;
        }

        protected override async void OnCurrentPackageChanged()
        {
            if (_searchResultPackage == null)
            {
                return;
            }

            UpdateInstalledVersions();

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
                    p.Providers = await AlternativePackageManagerProviders.CalculateAlternativePackageManagersAsync(
                        _packageManagerProviders,
                        Id,
                        p.NuGetProject);
                }
            }
        }

        public override IEnumerable<NuGetProject> GetSelectedProjects(UserAction action)
        {
            var selectedProjects = new List<NuGetProject>();

            foreach (var project in _projects)
            {
                if (project.IsSelected == false)
                {
                    continue;
                }

                if (action.Action == NuGetProjectActionType.Install)
                {
                    // for install, the installed version can't be the same as the version to be installed.
                    if (VersionComparer.Default.Compare(
                        project.InstalledVersion,
                        action.Version) != 0)
                    {
                        selectedProjects.Add(project.NuGetProject);
                    }
                }
                else
                {
                    // for uninstall, the package must be already installed
                    if (project.InstalledVersion != null)
                    {
                        selectedProjects.Add(project.NuGetProject);
                    }
                }
            }

            return selectedProjects;
        }
    }
}