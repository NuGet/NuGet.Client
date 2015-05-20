// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The base class of PackageDetailControlModel and PackageSolutionDetailControlModel.
    /// When user selects an action, this triggers version list update.
    /// </summary>
    public abstract class DetailControlModel : INotifyPropertyChanged
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected IEnumerable<NuGetProject> _nugetProjects;

        // all versions of the _searchResultPackage
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<NuGetVersion> _allPackageVersions;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected SearchResultPackageMetadata _searchResultPackage;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected Filter _filter;

        private Dictionary<NuGetVersion, DetailedPackageMetadata> _metadataDict;

        protected DetailControlModel(IEnumerable<NuGetProject> nugetProjects)
        {
            _nugetProjects = nugetProjects;
            _options = new Options();
        }

        public abstract IEnumerable<NuGetProject> SelectedProjects { get; }

        /// <summary>
        /// Sets the package to be displayed in the detail control.
        /// </summary>
        /// <param name="searchResultPackage">The package to be displayed.</param>
        /// <param name="filter">The current filter. This will used to select the default action.</param>
        public async virtual Task SetCurrentPackage(
            SearchResultPackageMetadata searchResultPackage,
            Filter filter)
        {
            _searchResultPackage = searchResultPackage;
            _filter = filter;
            OnPropertyChanged("Id");
            OnPropertyChanged("IconUrl");

            var versions = await searchResultPackage.Versions.Value;

            _allPackageVersions = versions.Select(v => v.Version).ToList();
            CreateActions();
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<PackageIdentity> InstalledPackages
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        var installedPackages = new List<Packaging.PackageReference>();
                        foreach (var project in _nugetProjects)
                        {
                            var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                            installedPackages.AddRange(projectInstalledPackages);
                        }
                        return installedPackages.Select(e => e.PackageIdentity).Distinct(PackageIdentity.Comparer);
                    });
            }
        }

        public virtual void Refresh()
        {
            CreateActions();
        }

        /// <summary>
        /// Whether or not the package can be installed
        /// </summary>
        protected abstract bool CanInstall();

        /// <summary>
        /// Whether or not the package can be updated to a new version (combined upgrade/downgrade scenario)
        /// </summary>
        protected abstract bool CanUpdate();

        /// <summary>
        /// Whether or not the package can be upgraded to a newer version
        /// </summary>
        protected abstract bool CanUpgrade();

        /// <summary>
        /// Whether or not the package can be uninstalled
        /// </summary>
        protected abstract bool CanUninstall();

        /// <summary>
        /// Whether or not the package can be downgraded to an older version
        /// </summary>
        protected abstract bool CanDowngrade();

        /// <summary>
        /// Whether or not the package can be consolidated onto a version used elsewhere
        /// </summary>
        protected abstract bool CanConsolidate();

        // Create the _actions list
        protected void CreateActions()
        {
            Actions = new List<string>();

            if (CanInstall())
            {
                Actions.Add(Resources.Action_Install);
            }

            if (CanUpgrade())
            {
                Actions.Add(Resources.Action_Upgrade);
            }

            if (CanUninstall())
            {
                Actions.Add(Resources.Action_Uninstall);
            }

            if (CanDowngrade())
            {
                Actions.Add(Resources.Action_Downgrade);
            }

            var canUpdate = CanUpdate();
            if (canUpdate)
            {
                Actions.Add(Resources.Action_Update);
            }

            if (CanConsolidate())
            {
                Actions.Add(Resources.Action_Consolidate);
            }

            if (Actions.Count > 0)
            {
                if (_filter == Filter.UpdatesAvailable && canUpdate)
                {
                    SelectedAction = Resources.Action_Update;
                }
                else
                {
                    SelectedAction = Actions[0];
                }
            }
            else
            {
                SelectedActionIsInstall = false;
            }

            OnPropertyChanged("Actions");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public List<string> Actions { get; private set; }

        public string Id
        {
            get { return _searchResultPackage.Id; }
        }

        public Uri IconUrl
        {
            get { return _searchResultPackage.IconUrl; }
        }

        private DetailedPackageMetadata _packageMetadata;

        public DetailedPackageMetadata PackageMetadata
        {
            get { return _packageMetadata; }
            set
            {
                if (_packageMetadata != value)
                {
                    _packageMetadata = value;
                    OnPropertyChanged("PackageMetadata");
                }
            }
        }

        private string _selectedAction;

        public string SelectedAction
        {
            get { return _selectedAction; }
            set
            {
                _selectedAction = value;
                SelectedActionIsInstall = (SelectedAction != Resources.Action_Uninstall);
                CreateVersions();
                OnPropertyChanged("SelectedAction");
            }
        }

        protected abstract void CreateVersions();

        // indicates whether the selected action is install or uninstall.
        private bool _selectedActionIsInstall;

        /// <summary>
        /// This is used by the UI to decide whether the install options or uninstall options are displayed.
        /// </summary>
        public bool SelectedActionIsInstall
        {
            get { return _selectedActionIsInstall; }
            set
            {
                if (_selectedActionIsInstall != value)
                {
                    _selectedActionIsInstall = value;
                    OnPropertyChanged("SelectedActionIsInstall");
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<VersionForDisplay> _versions;

        public List<VersionForDisplay> Versions
        {
            get { return _versions; }
        }

        private VersionForDisplay _selectedVersion;

        public VersionForDisplay SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                if (_selectedVersion != value)
                {
                    _selectedVersion = value;

                    DetailedPackageMetadata packageMetadata;
                    if (_metadataDict != null
                        && _metadataDict.TryGetValue(_selectedVersion.Version, out packageMetadata))
                    {
                        PackageMetadata = packageMetadata;
                    }
                    else
                    {
                        PackageMetadata = null;
                    }
                    OnSelectedVersionChanged();
                    OnPropertyChanged("SelectedVersion");
                }
            }
        }

        // Calculate the version to select among _versions and select it
        protected void SelectVersion()
        {
            if (_versions.Count == 0)
            {
                // there's nothing to select
                return;
            }

            VersionForDisplay versionToSelect = null;
            if (SelectedAction == Resources.Action_Install)
            {
                versionToSelect = _versions
                    .Where(v => v != null && v.Version.Equals(_searchResultPackage.Version))
                    .FirstOrDefault();
                if (versionToSelect == null)
                {
                    versionToSelect = _versions[0];
                }
            }
            else
            {
                versionToSelect = _versions[0];
            }

            if (versionToSelect != null)
            {
                SelectedVersion = versionToSelect;
            }
        }

        public async Task LoadPackageMetadaAsync(UIMetadataResource metadataResource, CancellationToken token)
        {
            var versions = await _searchResultPackage.Versions.Value;

            var downloadCountDict = versions.ToDictionary(
                v => v.Version,
                v => v.DownloadCount);

            var dict = new Dictionary<NuGetVersion, DetailedPackageMetadata>();
            if (metadataResource != null)
            {
                // load up the full details for each version
                try
                {
                    var metadata = await metadataResource.GetMetadata(Id, true, false, token);
                    foreach (var item in metadata)
                    {
                        if (!dict.ContainsKey(item.Identity.Version))
                        {
                            int? downloadCount;
                            if (!downloadCountDict.TryGetValue(item.Identity.Version, out downloadCount))
                            {
                                downloadCount = 0;
                            }
                            dict.Add(item.Identity.Version, new DetailedPackageMetadata(item, downloadCount));
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ignore failures.
                }
            }

            _metadataDict = dict;

            DetailedPackageMetadata p;
            if (SelectedVersion != null
                && _metadataDict.TryGetValue(SelectedVersion.Version, out p))
            {
                PackageMetadata = p;
            }
        }

        protected abstract void OnSelectedVersionChanged();

        public abstract bool IsSolution { get; }

        private Options _options;

        public Options Options
        {
            get { return _options; }
            set
            {
                _options = value;
                OnPropertyChanged("Options");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public IUIBrushes UIBrushes
        {
            get { return null; }
        }
    }
}
