using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using NuGet.ProjectManagement;
using NuGet.Packaging.Core;
using System.Threading;
using NuGet.Protocol.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The base class of PackageDetailControlModel and PackageSolutionDetailControlModel.
    /// When user selects an action, this triggers version list update.
    /// </summary>
    internal abstract class DetailControlModel : INotifyPropertyChanged
    {
        protected IEnumerable<NuGetProject> _nugetProjects;
        
        // all versions of the _searchResultPackage
        protected List<NuGetVersion> _allPackages;

        protected SearchResultPackageMetadata _searchResultPackage;

        private Dictionary<NuGetVersion, DetailedPackageMetadata> _metadataDict;

        public DetailControlModel(IEnumerable<NuGetProject> nugetProjects)
        {
            _nugetProjects = nugetProjects;
            _options = new UI.Options();
        }

        abstract public IEnumerable<NuGetProject> SelectedProjects
        {
            get;
        }
        public virtual void SetCurrentPackage(SearchResultPackageMetadata searchResultPackage)
        {
            _searchResultPackage = searchResultPackage;
            OnPropertyChanged("Id");
            OnPropertyChanged("IconUrl");

            _allPackages = searchResultPackage.Versions.Select(v => v.Version).ToList();
            CreateActions();
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<PackageIdentity> InstalledPackages
        {
            get
            {
                List<NuGet.Packaging.PackageReference> installedPackages = new List<Packaging.PackageReference>();
                foreach(var project in _nugetProjects)
                {
                    var task = project.GetInstalledPackagesAsync(CancellationToken.None);
                    task.Wait();
                    installedPackages.AddRange(task.Result);
                }
                return installedPackages.Select(e => e.PackageIdentity).Distinct(PackageIdentity.Comparer);
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
            _actions = new List<string>();

            if (CanInstall())
            {
                _actions.Add(Resources.Action_Install);
            }

            if (CanUpgrade())
            {
                _actions.Add(Resources.Action_Upgrade);
            }

            if (CanUninstall())
            {
                _actions.Add(Resources.Action_Uninstall);
            }

            if (CanDowngrade())
            {
                _actions.Add(Resources.Action_Downgrade);
            }

            if (CanUpdate())
            {
                _actions.Add(Resources.Action_Update);
            }

            if (CanConsolidate())
            {
                _actions.Add(Resources.Action_Consolidate);
            }

            if (_actions.Count > 0)
            {
                SelectedAction = _actions[0];
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
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private List<string> _actions;

        public List<string> Actions
        {
            get
            {
                return _actions;
            }
        }

        public string Id
        {
            get
            {
                return _searchResultPackage.Id;
            }
        }

        public Uri IconUrl
        {
            get
            {
                return _searchResultPackage.IconUrl;
            }
        }

        private DetailedPackageMetadata _packageMetadata;

        public DetailedPackageMetadata PackageMetadata
        {
            get 
            { 
                return _packageMetadata; 
            }
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
            get
            {
                return _selectedAction;
            }
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
        bool _selectedActionIsInstall;

        public bool SelectedActionIsInstall
        {
            get
            {
                return _selectedActionIsInstall;
            }
            set
            {
                if (_selectedActionIsInstall != value)
                {
                    _selectedActionIsInstall = value;
                    OnPropertyChanged("SelectedActionIsInstall");
                }
            }
        }

        protected List<VersionForDisplay> _versions;

        public List<VersionForDisplay> Versions
        {
            get
            {
                return _versions;
            }
        }

        private VersionForDisplay _selectedVersion;

        public VersionForDisplay SelectedVersion
        {
            get
            {
                return _selectedVersion;
            }
            set
            {
                if (_selectedVersion != value)
                {
                    _selectedVersion = value;

                    DetailedPackageMetadata packageMetadata;
                    if (_metadataDict != null &&
                        _metadataDict.TryGetValue(_selectedVersion.Version, out packageMetadata))
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

        // Caculate the version to select among _versions and select it
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
            var downloadCountDict = _searchResultPackage.Versions.ToDictionary(
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
                            int downloadCount;
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
            if (SelectedVersion != null &&
                _metadataDict.TryGetValue(SelectedVersion.Version, out p))
            {
                PackageMetadata = p;
            }
        }

        protected abstract void OnSelectedVersionChanged();

        public abstract bool IsSolution
        {
            get;
        }

        private Options _options;

        public Options Options
        {
            get
            {
                return _options;
            }
            set
            {
                _options = value;
                OnPropertyChanged("Options");
            }
        }

        public IUIBrushes UIBrushes
        {
            get
            {
                return null;
            }
        }
    }
}
