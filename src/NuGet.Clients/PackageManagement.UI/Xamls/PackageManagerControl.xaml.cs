﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagerControl.xaml
    /// </summary>
    public partial class PackageManagerControl : UserControl, IVsWindowSearch
    {
        private readonly bool _initialized;

        // used to prevent starting new search when we update the package sources
        // list in response to PackageSourcesChanged event.
        private bool _dontStartNewSearch;

        private PackageRestoreBar _restoreBar;

        private RestartRequestBar _restartBar;

        private readonly IVsWindowSearchHost _windowSearchHost;
        private readonly IVsWindowSearchHostFactory _windowSearchHostFactory;

        private readonly DetailControlModel _detailModel;

        private readonly Dispatcher _uiDispatcher;

        private bool _missingPackageStatus;

        public PackageManagerModel Model { get; }

        public PackageManagerControl(
            PackageManagerModel model,
            Configuration.ISettings nugetSettings,
            IVsWindowSearchHostFactory searchFactory,
            IVsShell4 vsShell)
        {
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            Model = model;
            if (!Model.IsSolution)
            {
                _detailModel = new PackageDetailControlModel(Model.Context.Projects);
            }
            else
            {
                _detailModel = new PackageSolutionDetailControlModel(
                    Model.Context.SolutionManager,
                    Model.Context.Projects);
            }

            InitializeComponent();

            _windowSearchHostFactory = searchFactory;
            if (_windowSearchHostFactory != null)
            {
                _windowSearchHost = _windowSearchHostFactory.CreateWindowSearchHost(
                    _topPanel.SearchControlParent);
                _windowSearchHost.SetupSearch(this);
                _windowSearchHost.IsVisible = true;
            }

            AddRestoreBar();

            AddRestartRequestBar(vsShell);

            _packageDetail.Control = this;
            _packageDetail.Visibility = Visibility.Hidden;

            SetTitle();

            var settings = LoadSettings();
            InitializeFilterList(settings);
            InitSourceRepoList(settings);
            ApplySettings(settings, nugetSettings);

            _initialized = true;

            // UI is initialized. Start the first search
            _packageList.CheckBoxesEnabled = _topPanel.Filter == Filter.UpdatesAvailable;
            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
            RefreshAvailableUpdatesCount();

            // register with the UI controller
            var controller = model.UIController as NuGetUI;
            if (controller != null)
            {
                controller.PackageManagerControl = this;
            }

            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesChanged += Sources_PackageSourcesChanged;

            if (IsUILegalDisclaimerSuppressed())
            {
                _legalDisclaimer.Visibility = Visibility.Collapsed;
            }

            _missingPackageStatus = false;
        }

        public PackageRestoreBar RestoreBar => _restoreBar;

        private void InitializeFilterList(UserSettings settings)
        {
            if (settings != null)
            {
                _topPanel.SelectFilter(settings.SelectedFilter);
            }
        }

        private static bool IsUILegalDisclaimerSuppressed()
        {
            return RegistrySettingUtility.GetBooleanSetting(Constants.SuppressUIDisclaimerRegistryName);
        }

        protected static DependencyBehavior GetDependencyBehaviorFromConfig(
            Configuration.ISettings nugetSettings)
        {
            var dependencySetting = nugetSettings.GetValue("config", "dependencyversion");
            DependencyBehavior behavior;
            var success = Enum.TryParse(dependencySetting, true, out behavior);
            if (success)
            {
                return behavior;
            }
            // Default to Lowest
            return DependencyBehavior.Lowest;
        }

        private void SetSelectedDepencyBehavior(DependencyBehavior dependencyBehavior)
        {
            var selectedDependencyBehavior = _detailModel.Options.DependencyBehaviors
                .FirstOrDefault(d => d.Behavior == dependencyBehavior);
            if (selectedDependencyBehavior != null)
            {
                _detailModel.Options.SelectedDependencyBehavior = selectedDependencyBehavior;
            }
        }

        public void ApplyShowPreviewSetting(bool show)
        {
            _detailModel.Options.ShowPreviewWindow = show;
        }

        private void ApplySettings(
            UserSettings settings,
            Configuration.ISettings nugetSettings)
        {
            if (settings == null)
            {
                if (nugetSettings == null)
                {
                    return;
                }

                // set depency behavior to the value from nugetSettings
                SetSelectedDepencyBehavior(GetDependencyBehaviorFromConfig(nugetSettings));
                return;
            }

            _detailModel.Options.ShowPreviewWindow = settings.ShowPreviewWindow;
            _detailModel.Options.RemoveDependencies = settings.RemoveDependencies;
            _detailModel.Options.ForceRemove = settings.ForceRemove;
            _topPanel.CheckboxPrerelease.IsChecked = settings.IncludePrerelease;

            SetSelectedDepencyBehavior(settings.DependencyBehavior);

            var selectedFileConflictAction = _detailModel.Options.FileConflictActions.
                FirstOrDefault(a => a.Action == settings.FileConflictAction);
            if (selectedFileConflictAction != null)
            {
                _detailModel.Options.SelectedFileConflictAction = selectedFileConflictAction;
            }
        }

        private IEnumerable<SourceRepository> GetEnabledSources()
        {
            return Model.Context.SourceProvider.GetRepositories().Where(s => s.PackageSource.IsEnabled);
        }

        private void Sources_PackageSourcesChanged(object sender, EventArgs e)
        {
            // Set _dontStartNewSearch to true to prevent a new search started in
            // _sourceRepoList_SelectionChanged(). This method will start the new
            // search when needed by itself.
            _dontStartNewSearch = true;
            try
            {
                var oldActiveSource = _topPanel.SourceRepoList.SelectedItem as SourceRepository;
                var newSources = GetEnabledSources();

                // Update the source repo list with the new value.
                _topPanel.SourceRepoList.Items.Clear();
                foreach (var source in newSources)
                {
                    _topPanel.SourceRepoList.Items.Add(source);
                }

                SetNewActiveSource(newSources, oldActiveSource);

                // force a new search explicitly if active source has changed
                if ((oldActiveSource == null && ActiveSource != null)
                    || (oldActiveSource != null && ActiveSource == null)
                    || (oldActiveSource != null && ActiveSource != null &&
                        !StringComparer.OrdinalIgnoreCase.Equals(
                            oldActiveSource.PackageSource.Source,
                            ActiveSource.PackageSource.Source)))
                {
                    SaveSettings();
                    SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
                    RefreshAvailableUpdatesCount();
                }
            }
            finally
            {
                _dontStartNewSearch = false;
            }
        }

        private string GetSettingsKey()
        {
            string key;
            if (Model.Context.Projects.Count() == 1)
            {
                var project = Model.Context.Projects.First();
                string projectName = null;
                if (!project.TryGetMetadata(NuGetProjectMetadataKeys.Name, out projectName))
                {
                    projectName = "unknown";
                }
                key = "project:" + projectName;
            }
            else
            {
                key = "solution";
            }

            return key;
        }

        // Save the settings of this doc window in the UIContext. Note that the settings
        // are not guaranteed to be persisted. We need to call Model.Context.SaveSettings()
        // to persist the settings.
        public void SaveSettings()
        {
            var settings = new UserSettings();
            if (ActiveSource != null)
            {
                settings.SourceRepository = ActiveSource.PackageSource.Name;
            }

            settings.ShowPreviewWindow = _detailModel.Options.ShowPreviewWindow;
            settings.RemoveDependencies = _detailModel.Options.RemoveDependencies;
            settings.ForceRemove = _detailModel.Options.ForceRemove;
            settings.DependencyBehavior = _detailModel.Options.SelectedDependencyBehavior.Behavior;
            settings.FileConflictAction = _detailModel.Options.SelectedFileConflictAction.Action;
            settings.IncludePrerelease = _topPanel.CheckboxPrerelease.IsChecked == true;
            settings.SelectedFilter = _topPanel.Filter;

            Model.Context.AddSettings(GetSettingsKey(), settings);
        }

        private UserSettings LoadSettings()
        {
            var settings = Model.Context.GetSettings(GetSettingsKey());
            if (PreviewWindow.IsDoNotShowPreviewWindowEnabled())
            {
                settings.ShowPreviewWindow = false;
            }

            return settings;
        }

        /// <summary>
        /// Calculate the active source after the list of sources have been changed.
        /// </summary>
        /// <param name="newSources">The current list of sources.</param>
        /// <param name="oldActiveSource">The old active source.</param>
        private void SetNewActiveSource(IEnumerable<SourceRepository> newSources, SourceRepository oldActiveSource)
        {
            if (!newSources.Any())
            {
                ActiveSource = null;
            }
            else
            {
                if (oldActiveSource == null)
                {
                    // use the first enabled source as the active source
                    ActiveSource = newSources.FirstOrDefault();
                }
                else
                {
                    var s = newSources.FirstOrDefault(repo => StringComparer.CurrentCultureIgnoreCase.Equals(
                        repo.PackageSource.Name, oldActiveSource.PackageSource.Name));
                    if (s == null)
                    {
                        // the old active source does not exist any more. In this case,
                        // use the first eneabled source as the active source.
                        ActiveSource = newSources.FirstOrDefault();
                    }
                    else
                    {
                        // the old active source still exists. Keep it as the active source.
                        ActiveSource = s;
                    }
                }
            }

            _topPanel.SourceRepoList.SelectedItem = ActiveSource;
            if (ActiveSource != null)
            {
                Model.Context.SourceProvider.PackageSourceProvider.SaveActivePackageSource(ActiveSource.PackageSource);
            }
        }

        private void AddRestoreBar()
        {
            if (Model.Context.PackageRestoreManager != null)
            {
                _restoreBar = new PackageRestoreBar(Model.Context.SolutionManager, Model.Context.PackageRestoreManager);
                _restoreBar.SetValue(Grid.RowProperty, 0);

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

        private void AddRestartRequestBar(IVsShell4 vsRestarter)
        {
            if (Model.Context.PackageManager.DeleteOnRestartManager != null && vsRestarter != null)
            {
                _restartBar = new RestartRequestBar(Model.Context.PackageManager.DeleteOnRestartManager, vsRestarter);
                _restartBar.SetValue(Grid.RowProperty, 1);

                _root.Children.Add(_restartBar);
            }
        }

        private void RemoveRestartBar()
        {
            if (_restartBar != null)
            {
                _restartBar.CleanUp();

                Model.Context.PackageRestoreManager.PackagesMissingStatusChanged
                    -= packageRestoreManager_PackagesMissingStatusChanged;
            }
        }

        private void packageRestoreManager_PackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            // TODO: PackageRestoreManager fires this event even when solution is closed.
            // Don't do anything if solution is closed.
            // Add MissingPackageStatus to keep previous packageMissing status to avoid unnecessarily refresh
            // only when package is missing last time and is not missing this time, we need to refresh
            if (!e.PackagesMissing && _missingPackageStatus)
            {
                UpdateAfterPackagesMissingStatusChanged();
            }
            _missingPackageStatus = e.PackagesMissing;
        }

        // Refresh the UI after packages are restored.
        // Note that the PackagesMissingStatusChanged event can be fired from a non-UI thread in one case:
        // the VsSolutionManager.Init() method, which is scheduled on the thread pool. So this
        // method needs to use _uiDispatcher.
        private void UpdateAfterPackagesMissingStatusChanged()
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(UpdateAfterPackagesMissingStatusChanged);

                return;
            }

            UpdatePackageStatus();
            _packageDetail.Refresh();
        }

        private void SetTitle()
        {
            if (Model.IsSolution)
            {
                var name = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_Solution,
                    Model.SolutionName);
                _label.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_PackageManager,
                    name);
            }
            else
            {
                var project = Model.Context.Projects.First();
                string projectName = null;
                if (!project.TryGetMetadata(NuGetProjectMetadataKeys.Name, out projectName))
                {
                    projectName = "unknown";
                }

                _label.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_PackageManager,
                    projectName);
            }
        }

        private void InitSourceRepoList(UserSettings settings)
        {
            // init source repo list
            _topPanel.SourceRepoList.Items.Clear();
            var enabledSources = GetEnabledSources();
            foreach (var source in enabledSources)
            {
                _topPanel.SourceRepoList.Items.Add(source);
            }

            // get active source name.
            string activeSourceName = null;

            // try saved user settings first.
            if (settings != null
                && !string.IsNullOrEmpty(settings.SourceRepository))
            {
                activeSourceName = settings.SourceRepository;
            }
            else
            {
                // no user settings found. Then use the active source from PackageSourceProvider.
                activeSourceName = Model.Context.SourceProvider.PackageSourceProvider.ActivePackageSourceName;
            }

            if (activeSourceName != null)
            {
                ActiveSource = enabledSources
                    .FirstOrDefault(s => activeSourceName.Equals(s.PackageSource.Name, StringComparison.CurrentCultureIgnoreCase));
            }

            if (ActiveSource == null)
            {
                ActiveSource = enabledSources.FirstOrDefault();
            }

            if (ActiveSource != null)
            {
                _topPanel.SourceRepoList.SelectedItem = ActiveSource;
            }
        }

        private bool ShowInstalled
        {
            get
            {
                return _topPanel.Filter == Filter.Installed;
            }
        }

        private bool ShowUpdatesAvailable
        {
            get
            {
                return _topPanel.Filter == Filter.UpdatesAvailable;
            }
        }

        public bool IncludePrerelease
        {
            get { return _topPanel.CheckboxPrerelease.IsChecked == true; }
        }

        internal SourceRepository ActiveSource { get; private set; }

        /// <summary>
        /// This method is called from several event handlers. So, consolidating the use of JTF.Run in this method
        /// </summary>
        private void SearchPackageInActivePackageSource(string searchText)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    var option = new PackageLoaderOption(_topPanel.Filter, IncludePrerelease);                    
                    var loader = new PackageLoader(
                        option,
                        Model.IsSolution,
                        Model.Context.PackageManager,
                        Model.Context.Projects,
                        ActiveSource,
                        searchText);
                    await loader.InitializeAsync();
                    await _packageList.LoadAsync(loader);
                });
        }

        private void RefreshAvailableUpdatesCount()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                _topPanel._labelUpgradeAvailable.Count = 0;
                var updatesLoader = new PackageLoader(
                    new PackageLoaderOption(Filter.UpdatesAvailable, IncludePrerelease),
                    Model.IsSolution,
                    Model.Context.PackageManager,
                    Model.Context.Projects,
                    ActiveSource,
                    String.Empty);
                await updatesLoader.InitializeAsync();
                int updatesCount = await updatesLoader.CreatePackagesWithUpdatesAsync(CancellationToken.None);
                _topPanel._labelUpgradeAvailable.Count = updatesCount;
            });
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            Model.UIController.LaunchNuGetOptionsDialog();
        }

        private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(UpdateDetailPaneAsync);
        }

        /// <summary>
        /// Updates the detail pane based on the selected package
        /// </summary>
        private async Task UpdateDetailPaneAsync()
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
                await _detailModel.SetCurrentPackage(
                    selectedPackage,
                    _topPanel.Filter);

                _packageDetail.DataContext = _detailModel;
                _packageDetail.ScrollToHome();

                var uiMetadataResource = await ActiveSource.GetResourceAsync<UIMetadataResource>();
                await _detailModel.LoadPackageMetadaAsync(uiMetadataResource, CancellationToken.None);
            }
        }

        private static string GetPackageSourceTooltip(Configuration.PackageSource packageSource)
        {
            if (string.IsNullOrEmpty(packageSource.Description))
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} - {1}",
                    packageSource.Name,
                    packageSource.Source);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} - {1} - {2}",
                packageSource.Name,
                packageSource.Description,
                packageSource.Source);
        }

        private void SourceRepoList_SelectionChanged(object sender, EventArgs e)
        {
            if (_dontStartNewSearch || !_initialized)
            {
                return;
            }

            ActiveSource = _topPanel.SourceRepoList.SelectedItem as SourceRepository;
            if (ActiveSource != null)
            {
                _topPanel.SourceToolTip.Visibility = Visibility.Visible;
                _topPanel.SourceToolTip.DataContext = GetPackageSourceTooltip(ActiveSource.PackageSource);

                Model.Context.SourceProvider.PackageSourceProvider.SaveActivePackageSource(ActiveSource.PackageSource);
                SaveSettings();
                SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
                RefreshAvailableUpdatesCount();
            }
        }

        private void Filter_SelectionChanged(object sender, EventArgs e)
        {
            if (_initialized)
            {
                _packageList.CheckBoxesEnabled = _topPanel.Filter == Filter.UpdatesAvailable;
                SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
            }
        }

        internal void UpdatePackageStatus()
        {
            if (ShowInstalled || ShowUpdatesAvailable)
            {
                // refresh the whole package list
                SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
            }
            else
            {
                var installedPackages = GetInstalledPackages(Model.Context.Projects);

                // in this case, we only need to update PackageStatus of
                // existing items in the package list
                foreach (var item in _packageList.Items)
                {
                    var package = item as SearchResultPackageMetadata;
                    if (package == null)
                    {
                        continue;
                    }

                    package.BackgroundLoader = new Lazy<Task<BackgroundLoaderResult>>(async () => await GetPackageInfo(
                       package.Id,
                       installedPackages,
                       package.Versions));
                }
            }

            RefreshAvailableUpdatesCount();
        }

        private static IReadOnlyList<Packaging.PackageReference> GetInstalledPackages(IEnumerable<NuGetProject> projects)
        {
            var installedPackages = new List<Packaging.PackageReference>();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                foreach (var project in projects)
                {
                    var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                    installedPackages.AddRange(projectInstalledPackages);
                }
            });

            return installedPackages;
        }

        /// <summary>
        /// Gets the background result of the package specified by <paramref name="packageId" /> in
        /// the specified installation target.
        /// </summary>
        /// <param name="packageId">package id.</param>
        /// <param name="installedPackages">All installed pacakges.</param>
        /// <param name="allVersions">List of all versions of the package.</param>
        /// <returns>The background result of the package in the installation target.</returns>
        private static async Task<BackgroundLoaderResult> GetPackageInfo(
            string packageId,
            IReadOnlyList<Packaging.PackageReference> installedPackages,
            Lazy<Task<IEnumerable<VersionInfo>>> allVersions)
        {
            var versions = await allVersions.Value;

            var latestStableVersion = versions
                .Where(p => !p.Version.IsPrerelease)
                .Max(p => p.Version);

            // Get the minimum version installed in any target project/solution
            var minimumInstalledPackage = installedPackages
                .Where(p => p != null)
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId))
                .OrderBy(r => r.PackageIdentity.Version)
                .FirstOrDefault();

            BackgroundLoaderResult result;
            if (minimumInstalledPackage != null)
            {
                if (minimumInstalledPackage.PackageIdentity.Version < latestStableVersion)
                {
                    result = new BackgroundLoaderResult()
                    {
                        LatestVersion = latestStableVersion,
                        InstalledVersion = minimumInstalledPackage.PackageIdentity.Version,                        
                        Status = PackageStatus.UpdateAvailable
                    };
                }
                else
                {
                    result = new BackgroundLoaderResult()
                    {
                        InstalledVersion = minimumInstalledPackage.PackageIdentity.Version,
                        Status = PackageStatus.Installed
                    };
                }
            }
            else
            {
                result = new BackgroundLoaderResult()
                {
                    LatestVersion = latestStableVersion,
                    Status = PackageStatus.NotInstalled
                };
            }

            return result;
        }

        private void SearchControl_SearchStart(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
        }

        private void CheckboxPrerelease_CheckChanged(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            RegistrySettingUtility.SetBooleanSetting(
                Constants.IncludePrereleaseRegistryName, 
                _topPanel.CheckboxPrerelease.IsChecked == true);
            SearchPackageInActivePackageSource(_windowSearchHost.SearchQuery.SearchString);
            RefreshAvailableUpdatesCount();
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

            public string SearchString { get; set; }
        }

        public Guid Category
        {
            get { return Guid.Empty; }
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
            // pSearchSettings is of type SearchSettingsDataSource. We use dynamic here
            // so that the code can be run on both dev12 & dev14. If we use the type directly,
            // there will be type mismatch error.
            dynamic settings = pSearchSettings;
            settings.ControlMinWidth = (uint)_topPanel.SearchControlParent.MinWidth;
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
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            _windowSearchHost.Activate();
            _windowSearchHost.SearchAsync(new SearchQuery { SearchString = searchText });
        }

        public void CleanUp()
        {
            _windowSearchHost.TerminateSearch();
            RemoveRestoreBar();
            RemoveRestartBar();
            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesChanged -= Sources_PackageSourcesChanged;
            _packageList.SelectionChanged -= PackageList_SelectionChanged;
        }

        private void SuppressDisclaimerChecked(object sender, RoutedEventArgs e)
        {
            _legalDisclaimer.Visibility = Visibility.Collapsed;
            RegistrySettingUtility.SetBooleanSetting(Constants.SuppressUIDisclaimerRegistryName, true);
        }

        public void ExecuteAction(UserAction action, Action<NuGetUI> setOptions)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                this.IsEnabled = false;
                NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageOperationBegin);
                try
                {
                    var nugetUi = Model.UIController as NuGetUI;
                    if (nugetUi != null)
                    {
                        setOptions(nugetUi);
                    }

                    var restoreSucceded = await RestoreBar.UIRestorePackagesAsync(CancellationToken.None);
                    if (restoreSucceded)
                    {
                        await Task.Run(() =>
                            Model.Context.UIActionEngine.PerformActionAsync(
                                Model.UIController,
                                action,
                                this,
                                CancellationToken.None));
                    }
                }
                finally
                {
                    NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageOperationEnd);
                    IsEnabled = true;
                }
            });
        }

        private void ExecuteUninstallPackageCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var package = e.Parameter as SearchResultPackageMetadata;
            if (package == null || package.IsSolution || package.InstalledVersion == null)
            {
                return;
            }

            var action = new UserAction(NuGetProjectActionType.Uninstall, package.Id, package.InstalledVersion);
            ExecuteAction(
                action,
                nugetUi =>
                {
                    SetOptions(nugetUi, _detailModel.Options);
                    nugetUi.Projects = Model.Context.Projects;
                });
        }

        private void SetOptions(NuGetUI nugetUi, Options options)
        {
            nugetUi.FileConflictAction = options.SelectedFileConflictAction.Action;
            nugetUi.DependencyBehavior = options.SelectedDependencyBehavior.Behavior;
            nugetUi.RemoveDependencies = options.RemoveDependencies;
            nugetUi.ForceRemove = options.ForceRemove;
            nugetUi.DisplayPreviewWindow = options.ShowPreviewWindow;
        }

        private void ExecuteInstallPackageCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var package = e.Parameter as SearchResultPackageMetadata;
            if (package == null || package.IsSolution)
            {
                return;
            }

            var versionToInstall = package.LatestVersion ?? package.Version;
            var action = new UserAction(NuGetProjectActionType.Install, package.Id, versionToInstall);
            ExecuteAction(
                action,
                nugetUi =>
                {
                    SetOptions(nugetUi, _detailModel.Options);
                    nugetUi.Projects = Model.Context.Projects;
                });
        }

        private void PackageList_UpdateButtonClicked(object sender, EventArgs e)
        {
        }
    }
}