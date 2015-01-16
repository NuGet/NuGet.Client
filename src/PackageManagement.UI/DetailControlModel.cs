using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using NuGet.ProjectManagement;
using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using System.Threading;

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

        public virtual void SetCurrentPackage(SearchResultPackageMetadata searchResultPackage)
        {
            _searchResultPackage = searchResultPackage;
            OnPropertyChanged("Id");
            OnPropertyChanged("IconUrl");

            _allPackages = new List<NuGetVersion>(searchResultPackage.Versions);
            CreateActions();
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<PackageIdentity> InstalledPackages
        {
            get
            {
                return _nugetProjects.SelectMany(p => p.GetInstalledPackages()).Select(e => e.PackageIdentity).Distinct(PackageIdentity.Comparer);
            }
        }

        public virtual void Refresh()
        {
            CreateActions();
        }

        protected abstract bool CanUpdate();
        protected abstract bool CanInstall();
        protected abstract bool CanUninstall();
        protected abstract bool CanConsolidate();

        // Create the _actions list
        protected void CreateActions()
        {
            _actions = new List<string>();

            if (CanInstall())
            {
                _actions.Add(Resources.Action_Install);
            }

            if (CanUpdate())
            {
                _actions.Add(Resources.Action_Update);
            }

            if (CanUninstall())
            {
                _actions.Add(Resources.Action_Uninstall);
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
                SelectedActionIsInstall = SelectedAction != Resources.Action_Uninstall;
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

        public async Task LoadPackageMetadaAsync(UIMetadataResource metadataResource, CancellationToken token)
        {
            List<PackageIdentity> ids = new List<PackageIdentity>();

            foreach (var version in _versions.Where(e => e != null))
            {
                ids.Add(new PackageIdentity(Id, version.Version));
            }

            var dict = new Dictionary<NuGetVersion, DetailedPackageMetadata>();

            if (metadataResource != null)
            {
                // load up the full details for each version
                var metadata = await metadataResource.GetMetadata(ids, true, false, token);

                if (metadata != null)
                {
                    foreach (var item in metadata)
                    {
                        if (!dict.ContainsKey(item.Identity.Version))
                        {
                            dict.Add(item.Identity.Version, new DetailedPackageMetadata(item));
                        }
                    }
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
