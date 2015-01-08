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

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The base class of PackageDetailControlModel and PackageSolutionDetailControlModel.
    /// When user selects an action, this triggers version list update.
    /// </summary>
    public abstract class DetailControlModel : INotifyPropertyChanged
    {
        protected NuGetProject _target;
        protected List<NuGetVersion> _allPackages;
        protected UiSearchResultPackage _searchResultPackage;

        private Dictionary<NuGetVersion, UiPackageMetadata> _metadataDict;

        public DetailControlModel(
            NuGetProject target,
            UiSearchResultPackage searchResultPackage)
        {
            _target = target;
            _searchResultPackage = searchResultPackage;
            _allPackages = new List<NuGetVersion>(searchResultPackage.Versions);
            _options = new UI.Options();
            CreateActions();
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

        private UiPackageMetadata _packageMetadata;

        public UiPackageMetadata PackageMetadata
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

                    UiPackageMetadata packageMetadata;
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

        public async Task LoadPackageMetadaAsync(UIMetadataResource metadataResource)
        {
            List<PackageIdentity> ids = new List<PackageIdentity>();

            foreach (var version in _versions.Where(e => e != null))
            {
                ids.Add(new PackageIdentity(Id, version.Version));
            }

            var dict = new Dictionary<NuGetVersion, UiPackageMetadata>();

            // TODO: request data from the server

            if (metadataResource != null)
            {
                //var metadata = await metadataResource.GetMetadata(ids);

                //foreach (var item in metadata)
                //{
                //    dict.Add();
                //}
            }

            _metadataDict = dict;

            UiPackageMetadata p;
            if (SelectedVersion != null &&
                _metadataDict.TryGetValue(SelectedVersion.Version, out p))
            {
                PackageMetadata = p;
            }
        }

        protected abstract void OnSelectedVersionChanged();

        public bool IsSolution
        {
            get
            {
                // TODO: allow solution level
                return false;
            }
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
