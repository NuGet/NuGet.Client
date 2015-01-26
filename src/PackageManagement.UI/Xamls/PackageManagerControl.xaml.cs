using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagerControl.xaml
    /// </summary>
    public partial class PackageManagerControl : UserControl, IVsWindowSearch
    {
        private const int PageSize = 10;

        private bool _initialized;
        private SourceRepository _activeSource;

        // used to prevent starting new search when we update the package sources
        // list in response to PackageSourcesChanged event.
        private bool _dontStartNewSearch;

        // TODO: hook this back up
        private PackageRestoreBar _restoreBar;

        private IVsWindowSearchHost _windowSearchHost;
        private IVsWindowSearchHostFactory _windowSearchHostFactory;

        private DetailControlModel _detailModel;

        public PackageManagerModel Model { get; private set; }

        public PackageManagerControl(PackageManagerModel model)
            : this(model, new SimpleSearchBoxFactory())
        {
        }

        public PackageManagerControl(PackageManagerModel model, IVsWindowSearchHostFactory searchFactory)
        {
            Model = model;
            if (Model.Context.Projects.Count() == 1)
            {
                _detailModel = new PackageDetailControlModel(Model.Context.Projects);
            }
            else
            {
                _detailModel = new PackageSolutionDetailControlModel(Model.Context.Projects);
            }

            InitializeComponent();

            _windowSearchHostFactory = searchFactory;
            if (_windowSearchHostFactory != null)
            {
                _windowSearchHost = _windowSearchHostFactory.CreateWindowSearchHost(_searchControlParent);
                _windowSearchHost.SetupSearch(this);
                _windowSearchHost.IsVisible = true;
            }

            _filter.Items.Add(Resx.Resources.Filter_All);
            _filter.Items.Add(Resx.Resources.Filter_Installed);
            _filter.Items.Add(Resx.Resources.Filter_UpdateAvailable);

            AddRestoreBar();

            _packageDetail.Control = this;
            _packageDetail.Visibility = Visibility.Hidden;

            SetTitle();
            InitSourceRepoList();

            _initialized = true;

            // register with the UI controller
            NuGetUI controller = model.UIController as NuGetUI;
            if (controller != null)
            {
                controller.PackageManagerControl = this;
            }

            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesSaved += Sources_PackageSourcesChanged;
        }

        private IEnumerable<SourceRepository> EnabledSources
        {
            get
            {
                return Model.Context.SourceProvider.GetRepositories().Where(s => s.PackageSource.IsEnabled);
            }
        }

        private void Sources_PackageSourcesChanged(object sender, EventArgs e)
        {
            // Set _dontStartNewSearch to true to prevent a new search started in
            // _sourceRepoList_SelectionChanged(). This method will start the new
            // search when needed by itself.
            _dontStartNewSearch = true;
            try
            {
                var oldActiveSource = _sourceRepoList.SelectedItem as PackageSource;
                var newSources = new List<PackageSource>(EnabledSources.Select(s => s.PackageSource));

                // Update the source repo list with the new value.
                _sourceRepoList.Items.Clear();

                foreach (var source in newSources)
                {
                    _sourceRepoList.Items.Add(source);
                }

                if (oldActiveSource != null && newSources.Contains(oldActiveSource))
                {
                    // active source is not changed. Set _dontStartNewSearch to true
                    // to prevent a new search when _sourceRepoList.SelectedItem is set.
                    _sourceRepoList.SelectedItem = oldActiveSource;
                }
                else
                {
                    // active source changed.
                    _sourceRepoList.SelectedItem =
                        newSources.Count > 0 ?
                        newSources[0] :
                        null;

                    // start search explicitly.
                    SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
                }
            }
            finally
            {
                _dontStartNewSearch = false;
            }
        }

        private void AddRestoreBar()
        {
            if (Model.Context.PackageRestoreManager != null)
            {
                _restoreBar = new PackageRestoreBar(Model.Context.PackageRestoreManager);
                _root.Children.Add(_restoreBar);
                Model.Context.PackageRestoreManager.PackagesMissingStatusChanged += packageRestoreManager_PackagesMissingStatusChanged;
            }
        }

        private void RemoveRestoreBar()
        {
            if (_restoreBar != null)
            {
                _restoreBar.CleanUp();

                // TODO: clean this up during dispose also
                Model.Context.PackageRestoreManager.PackagesMissingStatusChanged -= packageRestoreManager_PackagesMissingStatusChanged;
            }
        }

        private void packageRestoreManager_PackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            // TODO: PackageRestoreManager fires this event even when solution is closed.
            // Don't do anything if solution is closed.
            if (!e.PackagesMissing)
            {
                // packages are restored. refresh the UI
                UpdatePackageStatus();
                _packageDetail.Refresh();
            }
        }

        private void SetTitle()
        {
            if (Model.Context.Projects.Count() > 1)
            {
                _label.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_PackageManager,
                    Model.SolutionName);
            }
            else
            {
                var project = Model.Context.Projects.First();
                string projectName = null;
                if (!project.TryGetMetadata<string>(NuGetProjectMetadataKeys.Name, out projectName))
                {
                    projectName = "unknown";
                }

                _label.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_PackageManager,
                    projectName);
            }
        }

        private void InitSourceRepoList()
        {
            // init source repo list
            _sourceRepoList.Items.Clear();
            foreach (var source in EnabledSources)
            {
                if (_activeSource == null)
                {
                    _activeSource = source;
                }

                _sourceRepoList.Items.Add(source.PackageSource);
            }

            if (_activeSource != null)
            {
                _sourceRepoList.SelectedItem = _activeSource.PackageSource;
            }
        }

        private bool ShowInstalled
        {
            get
            {
                return Resx.Resources.Filter_Installed.Equals(_filter.SelectedItem);
            }
        }

        private bool ShowUpdatesAvailable
        {
            get
            {
                return Resx.Resources.Filter_UpdateAvailable.Equals(_filter.SelectedItem);
            }
        }

        public bool IncludePrerelease
        {
            get
            {
                return _checkboxPrerelease.IsChecked == true;
            }
        }

        internal SourceRepository ActiveSource
        {
            get
            {
                return _activeSource;
            }
        }

        private void SearchPackageInActivePackageSource(string searchText)
        {
            Filter filter = Filter.All;
            if (Resx.Resources.Filter_Installed.Equals(_filter.SelectedItem))
            {
                filter = Filter.Installed;
            }
            else if (Resx.Resources.Filter_UpdateAvailable.Equals(_filter.SelectedItem))
            {
                filter = Filter.UpdatesAvailable;
            }

            if (_activeSource != null)
            {
                PackageLoaderOption option = new PackageLoaderOption(filter, IncludePrerelease);

                var loader = new PackageLoader(
                    option,
                    Model.Context.PackageManager,
                    Model.Context.Projects,
                    _activeSource,
                    searchText);
                _packageList.Loader = loader;
            }
        }

        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            Model.UIController.LaunchNuGetOptionsDialog();
        }

        private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetailPane();
        }

        /// <summary>
        /// Updates the detail pane based on the selected package
        /// </summary>
        private async void UpdateDetailPane()
        {
            var selectedPackage = _packageList.SelectedItem as SearchResultPackageMetadata;
            if (selectedPackage == null)
            {
                _packageDetail.Visibility = Visibility.Hidden;
                _packageDetail.DataContext = null;
            }
            else
            {
                _packageDetail.Visibility = Visibility.Visible;
                _detailModel.SetCurrentPackage(selectedPackage);
                _packageDetail.DataContext = _detailModel;
                _packageDetail.ScrollToHome();

                await _detailModel.LoadPackageMetadaAsync(await _activeSource.GetResourceAsync<UIMetadataResource>(), CancellationToken.None);
            }
        }

        private void _sourceRepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_dontStartNewSearch)
            {
                return;
            }

            var newSource = _sourceRepoList.SelectedItem as PackageSource;
            if (newSource != null)
            {
                _activeSource = EnabledSources.Where(s => s.PackageSource == _sourceRepoList.SelectedItem).SingleOrDefault();
            }

            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
        }

        private void _filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initialized)
            {
                SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
            }
        }

        internal void UpdatePackageStatus()
        {
            if (ShowInstalled || ShowUpdatesAvailable)
            {
                // refresh the whole package list
                _packageList.Reload();
            }
            else
            {
                // in this case, we only need to update PackageStatus of
                // existing items in the package list
                foreach (var item in _packageList.Items)
                {
                    var package = item as SearchResultPackageMetadata;
                    if (package == null)
                    {
                        continue;
                    }

                    package.Status = PackageManagerControl.GetPackageStatus(
                        package.Id,
                        Model.Context.Projects,
                        package.Versions);
                }
            }
        }

        /// <summary>
        /// Gets the status of the package specified by <paramref name="packageId"/> in
        /// the specified installation target.
        /// </summary>
        /// <param name="packageId">package id.</param>
        /// <param name="target">The installation target.</param>
        /// <param name="allVersions">List of all versions of the package.</param>
        /// <returns>The status of the package in the installation target.</returns>
        public static PackageStatus GetPackageStatus(
            string packageId,
            IEnumerable<NuGetProject> projects,
            IEnumerable<NuGetVersion> allVersions)
        {
            var latestStableVersion = allVersions
                .Where(p => !p.IsPrerelease)
                .Max(p => p);

            // Get the minimum version installed in any target project/solution
            var minimumInstalledPackage = projects.SelectMany(project => project.GetInstalledPackages())
                .Where(p => p != null)
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId))
                .OrderBy(r => r.PackageIdentity.Version)
                .FirstOrDefault();

            PackageStatus status;
            if (minimumInstalledPackage != null)
            {
                if (minimumInstalledPackage.PackageIdentity.Version < latestStableVersion)
                {
                    status = PackageStatus.UpdateAvailable;
                }
                else
                {
                    status = PackageStatus.Installed;
                }
            }
            else
            {
                status = PackageStatus.NotInstalled;
            }

            return status;
        }

        private void _searchControl_SearchStart(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
        }

        private void _checkboxPrerelease_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
        }

        internal class SearchQuery : IVsSearchQuery
        {
            public uint GetTokens(uint dwMaxTokens, IVsSearchToken[] rgpSearchTokens)
            {
                return 0;
            }

            public uint ParseError
            {
                get { return 0; }
            }

            public string SearchString
            {
                get;
                set;
            }
        }

        public Guid Category
        {
            get
            {
                return Guid.Empty;
            }
        }

        public void ClearSearch()
        {
            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
        }

        public IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            SearchPackageInActivePackageSource(pSearchQuery.SearchString);
            return null;
        }

        public bool OnNavigationKeyDown(uint dwNavigationKey, uint dwModifiers)
        {
            // We are not interesting in intercepting navigation keys, so return "not handled"
            return false;
        }

        public void ProvideSearchSettings(IVsUIDataSource pSearchSettings)
        {
            var settings = (SearchSettingsDataSource)pSearchSettings;
            settings.ControlMinWidth = (uint)_searchControlParent.MinWidth;
            settings.ControlMaxWidth = uint.MaxValue;
            settings.SearchWatermark = GetSearchText();
        }

        // Returns the text to be displayed in the search box.
        private string GetSearchText()
        {
            var focusOnSearchKeyGesture = (KeyGesture)InputBindings.OfType<KeyBinding>().First(
                x => x.Command == Commands.FocusOnSearchBox).Gesture;
            return string.Format(CultureInfo.CurrentCulture,
                Resx.Resources.Text_SearchBoxText,
                focusOnSearchKeyGesture.GetDisplayStringForCulture(CultureInfo.CurrentCulture));
        }

        public bool SearchEnabled
        {
            get { return true; }
        }

        public IVsEnumWindowSearchFilters SearchFiltersEnum
        {
            get { return null; }
        }

        public IVsEnumWindowSearchOptions SearchOptionsEnum
        {
            get { return null; }
        }

        private void FocusOnSearchBox_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _windowSearchHost.Activate();
        }

        public void Search(string searchText)
        {
            if (String.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            _windowSearchHost.Activate();
            _windowSearchHost.SearchAsync(new SearchQuery() { SearchString = searchText });
        }

        public void CleanUp()
        {
            _windowSearchHost.TerminateSearch();
            RemoveRestoreBar();
        }
    }
}