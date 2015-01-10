using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        //private IConsole _outputConsole;

        internal IUserInterfaceService UI { get; private set; }

        //private PackageRestoreBar _restoreBar;
        //private IPackageRestoreManager _packageRestoreManager;

        private IVsWindowSearchHost _windowSearchHost;
        private IVsWindowSearchHostFactory _windowSearchHostFactory;

        public PackageManagerModel Model { get; private set; }

        public SourceRepositoryProvider Sources
        {
            get
            {
                return Model.Sources;
            }
        }

        public IEnumerable<NuGetProject> Projects
        {
            get
            {
                return new NuGetProject[] { Model.Target };
            }
        }

        public PackageManagerControl(PackageManagerModel model, IUserInterfaceService ui)
            : this(model, ui, new SimpleSearchBoxFactory())
        {

        }

        public PackageManagerControl(PackageManagerModel model, IUserInterfaceService ui, IVsWindowSearchHostFactory searchFactory)
        {
            _windowSearchHostFactory = searchFactory;

            if (_windowSearchHostFactory != null)
            {
                _windowSearchHost = _windowSearchHostFactory.CreateWindowSearchHost(_searchControlParent);
                _windowSearchHost.SetupSearch(this);
                _windowSearchHost.IsVisible = true;
            }

            UI = ui;
            Model = model;

            InitializeComponent();

            _filter.Items.Add(Resx.Resources.Filter_All);
            _filter.Items.Add(Resx.Resources.Filter_Installed);
            _filter.Items.Add(Resx.Resources.Filter_UpdateAvailable);

            //_packageRestoreManager = ServiceLocator.GetInstance<IPackageRestoreManager>();
            AddRestoreBar();

            _packageDetail.Control = this;

            //var outputConsoleProvider = ServiceLocator.GetInstance<IOutputConsoleProvider>();
            //_outputConsole = outputConsoleProvider.CreateOutputConsole(requirePowerShellHost: false);

            InitSourceRepoList();

            _initialized = true;

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
                var newSources = new List<PackageSource>(Sources.GetRepositories().Select(s => s.PackageSource));

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
            //_restoreBar = new PackageRestoreBar(_packageRestoreManager);
            //_root.Children.Add(_restoreBar);
            //_packageRestoreManager.PackagesMissingStatusChanged += packageRestoreManager_PackagesMissingStatusChanged;
        }

        private void RemoveRestoreBar()
        {
            //if (_restoreBar != null)
            //{
            //    _restoreBar.CleanUp();
            //    _packageRestoreManager.PackagesMissingStatusChanged -= packageRestoreManager_PackagesMissingStatusChanged;
            //}
        }

        //private void packageRestoreManager_PackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        //{
        //    // PackageRestoreManager fires this event even when solution is closed.
        //    // Don't do anything if solution is closed.
        //    //if (!Target.IsAvailable)
        //    //{
        //    //    return;
        //    //}

        //    //if (!e.PackagesMissing)
        //    //{
        //    //    // packages are restored. Update the UI
        //    //    if (Target.IsSolution)
        //    //    {
        //    //        // TODO: update UI here
        //    //    }
        //    //    else
        //    //    {
        //    //        // TODO: update UI here
        //    //    }
        //    //}
        //}

        private void InitSourceRepoList()
        {
            // TODO: get this from the projects
            _label.Text = string.Format(
                CultureInfo.CurrentCulture,
                Resx.Resources.Label_PackageManager,
                "<Project>");

            // init source repo list
            _sourceRepoList.Items.Clear();
            foreach (var source in Sources.GetRepositories())
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

        internal SourceRepository CreateActiveRepository()
        {
            return _activeSource;
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
                    Projects,
                    _activeSource,
                    searchText);
                _packageList.Loader = loader;
            }
        }

        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            UI.LaunchNuGetOptionsDialog();
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
            var selectedPackage = _packageList.SelectedItem as UiSearchResultPackage;
            if (selectedPackage == null)
            {
                _packageDetail.DataContext = null;
            }
            else
            {
                DetailControlModel newModel;
                //if (Target.IsSolution)
                //{
                //    newModel = new PackageSolutionDetailControlModel(
                //        (VsSolution)Target,
                //        selectedPackage);
                //}

                // project level model
                // TODO: pass in the list instead of the first one
                newModel = new PackageDetailControlModel(
                        Projects.SingleOrDefault(),
                        selectedPackage);

                var oldModel = _packageDetail.DataContext as DetailControlModel;
                if (oldModel != null)
                {
                    newModel.Options = oldModel.Options;
                }
                _packageDetail.DataContext = newModel;
                _packageDetail.ScrollToHome();


                await newModel.LoadPackageMetadaAsync(await _activeSource.GetResourceAsync<UIMetadataResource>(), CancellationToken.None);
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
                _activeSource = Sources.GetRepositories().Where(s => s.PackageSource == _sourceRepoList.SelectedItem).SingleOrDefault();
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
                    var package = item as UiSearchResultPackage;
                    if (package == null)
                    {
                        continue;
                    }

                    package.Status = PackageManagerControl.GetPackageStatus(
                        package.Id,
                        Projects,
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

        public bool ShowLicenseAgreement(IEnumerable<PackageAction> operations)
        {
            throw new NotImplementedException();
            return false;

            //var licensePackages = operations.Where(op =>
            //    op.ActionType == PackageActionType.Install &&
            //    op.Package.re);

            //// display license window if necessary
            //if (licensePackages.Any())
            //{
            //    // Hacky distinct without writing a custom comparer
            //    var licenseModels = licensePackages
            //        .GroupBy(a => Tuple.Create(a.PackageIdentity.Id, a.PackageIdentity.Version.ToNormalizedString()))
            //        .Select(g =>
            //        {
            //            dynamic p = g.First().Package;
            //            string licenseUrl = (string)p.licenseUrl;
            //            string id = (string)p.id;
            //            string authors = (string)p.authors;

            //            return new PackageLicenseInfo(
            //                id,
            //                licenseUrl == null ? null : new Uri(licenseUrl),
            //                authors);
            //        })
            //        .Where(pli => pli.LicenseUrl != null); // Shouldn't get nulls, but just in case

            //    bool accepted = this.UI.PromptForLicenseAcceptance(licenseModels);
            //    if (!accepted)
            //    {
            //        return false;
            //    }
            //}

            //return true;
        }

        /// <summary>
        /// Shows the preveiw window for the actions.
        /// </summary>
        /// <param name="actions">actions to preview.</param>
        /// <returns>True if nuget should continue to perform the actions. Otherwise false.</returns>
        private bool PreviewActions(IEnumerable<PackageAction> actions)
        {
            var w = new PreviewWindow();
            w.DataContext = new PreviewWindowModel(Enumerable.Empty<PreviewResult>());
            return w.ShowModal() == true;
        }

        private void ActivateOutputWindow()
        {
            //var uiShell = ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>();
            //if (uiShell == null)
            //{
            //    return;
            //}

            //var guid = new Guid(EnvDTE.Constants.vsWindowKindOutput);
            //IVsWindowFrame f = null;
            //uiShell.FindToolWindow(0, ref guid, out f);
            //if (f == null)
            //{
            //    return;
            //}

            //f.Show();
        }

        // perform the user selected action
        internal async void PerformAction(DetailControl detailControl)
        {
            bool acceptLicense = ShowLicenseAgreement(null);
            if (!acceptLicense)
            {
                return;
            }

            ActivateOutputWindow();
            //_outputConsole.Clear();
            //var progressDialog = new ProgressDialog(_outputConsole);
            //progressDialog.Owner = Window.GetWindow(this);
            //progressDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //progressDialog.FileConflictAction = detailControl.FileConflictAction;
            //progressDialog.Show();

            //try
            //{
            //    var actions = await detailControl.ResolveActionsAsync(progressDialog);

            //    // show preview
            //    var model = (DetailControlModel)_packageDetail.DataContext;
            //    if (model.Options.ShowPreviewWindow)
            //    {
            //        var shouldContinue = PreviewActions(actions);
            //        if (!shouldContinue)
            //        {
            //            return;
            //        }
            //    }

            //    // show license agreeement
            //    bool acceptLicense = ShowLicenseAgreement(actions);
            //    if (!acceptLicense)
            //    {
            //        return;
            //    }

            //    // Create the executor and execute the actions
            //    var userAction = detailControl.GetUserAction();
            //    var executor = new ActionExecutor();
            //    await Task.Run(
            //        () =>
            //        {
            //            executor.ExecuteActions(actions, progressDialog, userAction);
            //        });

            //    UpdatePackageStatus();
            //    detailControl.Refresh();
            //}
            //catch (Exception ex)
            //{
            //    var errorDialog = new ErrorReportingDialog(
            //        ex.Message,
            //        ex.ToString());
            //    errorDialog.ShowModal();
            //}
            //finally
            //{
            //    progressDialog.CloseWindow();
            //}
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