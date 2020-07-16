// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.Telemetry;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Resx = NuGet.PackageManagement.UI;
using VSThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;
using Task = System.Threading.Tasks.Task;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Protocol;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagerControl.xaml
    /// </summary>
    public partial class PackageManagerControl : UserControl, IVsWindowSearch
    {
        private readonly bool _initialized;

        private CancellationTokenSource _refreshCts;
        internal CancellationTokenSource _loadCts;

        // used to prevent starting new search when we update the package sources
        // list in response to PackageSourcesChanged event.
        private bool _dontStartNewSearch;

        // When executing a UI operation, we disable the PM UI and ignore any refresh requests.
        // This tells the operation execution part that it needs to trigger a refresh when done.
        private bool _isRefreshRequired;

        // Signifies where an action is being executed. Should be updated in a coordinated fashion with IsEnabled
        private bool _isExecutingAction;
        private RestartRequestBar _restartBar;

        public PackageRestoreBar RestoreBar { get; private set; }

        private PRMigratorBar _migratorBar;

        private readonly IVsWindowSearchHost _windowSearchHost;
        private readonly IVsWindowSearchHostFactory _windowSearchHostFactory;

        internal readonly DetailControlModel _detailModel;

        private bool _missingPackageStatus;
        private readonly INuGetUILogger _uiLogger;
        private bool _loadedAndInitialized = false;
        private bool _recommendPackages = false;

        private (string modelVersion, string vsixVersion)? _recommenderVersion;

        public PackageManagerModel Model { get; }

        public ISettings Settings { get; }

        internal ItemFilter ActiveFilter { get => _topPanel.Filter; set => _topPanel.SelectFilter(value); }

        internal InfiniteScrollList PackageList { get => _packageList; }

        internal PackageSourceMoniker SelectedSource
        {
            get
            {
                return _topPanel.SourceRepoList.SelectedItem as PackageSourceMoniker;
            }
            set
            {
                _topPanel.SourceRepoList.SelectedItem = value;
            }
        }

        internal IEnumerable<PackageSourceMoniker> PackageSources => _topPanel.SourceRepoList.Items.OfType<PackageSourceMoniker>();

        internal IEnumerable<SourceRepository> ActiveSources => SelectedSource?.SourceRepositories ?? Enumerable.Empty<SourceRepository>();

        internal event EventHandler _actionCompleted;

        public bool IncludePrerelease => _topPanel.CheckboxPrerelease.IsChecked == true;

        private readonly Guid _sessionGuid = Guid.NewGuid();
        private readonly Stopwatch _sinceLastRefresh;
        private bool _installedTabDataIsLoaded;
        private bool _updatesTabDataIsLoaded;

        private bool _forceRecommender;

        public PackageManagerControl(
            PackageManagerModel model,
            ISettings nugetSettings,
            IVsWindowSearchHostFactory searchFactory,
            IVsShell4 vsShell,
            INuGetUILogger uiLogger = null)
        {
            VSThreadHelper.ThrowIfNotOnUIThread();
            _sinceLastRefresh = Stopwatch.StartNew();

            _uiLogger = uiLogger;
            Model = model;
            Settings = nugetSettings;
            if (!Model.IsSolution)
            {
                _detailModel = new PackageDetailControlModel(Model.Context.SolutionManager, Model.Context.Projects);
            }
            else
            {
                _detailModel = new PackageSolutionDetailControlModel(
                    Model.Context.SolutionManager,
                    Model.Context.Projects,
                    Model.Context.PackageManagerProviders);
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

            _topPanel.IsSolution = Model.IsSolution;
            if (_topPanel.IsSolution)
            {
                _topPanel.CreateAndAddConsolidateTab();
            }
            var settings = LoadSettings();
            InitializeFilterList(settings);
            InitSourceRepoList(settings);
            ApplySettings(settings, nugetSettings);
            _initialized = true;

            // UI is initialized. Start the first search
            _packageList.CheckBoxesEnabled = _topPanel.Filter == ItemFilter.UpdatesAvailable;
            _packageList.IsSolution = Model.IsSolution;

            Loaded += PackageManagerLoaded;

            // register with the UI controller
            var controller = model.UIController as NuGetUI;
            if (controller != null)
            {
                controller.PackageManagerControl = this;
            }

            var solutionManager = Model.Context.SolutionManager;
            solutionManager.NuGetProjectAdded += SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRemoved += SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectUpdated += SolutionManager_ProjectsUpdated;
            solutionManager.NuGetProjectRenamed += SolutionManager_ProjectRenamed;
            solutionManager.ActionsExecuted += SolutionManager_ActionsExecuted;
            solutionManager.AfterNuGetCacheUpdated += SolutionManager_CacheUpdated;

            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesChanged += Sources_PackageSourcesChanged;

            Unloaded += PackageManagerUnloaded;

            if (IsUILegalDisclaimerSuppressed())
            {
                _legalDisclaimer.Visibility = Visibility.Collapsed;
            }

            _missingPackageStatus = false;

            // check if environment variable RecommendNuGetPackages to turn on recommendations is set to 1
            try
            {
                _forceRecommender = (Environment.GetEnvironmentVariable("NUGET_RECOMMEND_PACKAGES") == "1");
            }
            catch (SecurityException)
            {
                // don't make recommendations if we are not able to read the environment variable
            }
        }

        private void SolutionManager_ProjectsUpdated(object sender, NuGetProjectEventArgs e)
        {
            Model.Context.Projects = _detailModel.NuGetProjects;
        }

        private void SolutionManager_ProjectRenamed(object sender, NuGetProjectEventArgs e)
        {
            SolutionManager_ProjectsChanged(sender, e);
            if (!Model.IsSolution)
            {
                var currentNugetProject = Model.Context.Projects.First();
                string currentFullPath, newFullPath;
                currentNugetProject.TryGetMetadata(NuGetProjectMetadataKeys.FullPath, out currentFullPath);
                e.NuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.FullPath, out newFullPath);
                if (currentFullPath == newFullPath)
                {
                    Model.Context.Projects = new[] { e.NuGetProject };
                    SetTitle();
                }
            }

        }

        private void SolutionManager_ProjectsChanged(object sender, NuGetProjectEventArgs e)
        {
            var timeSpan = GetTimeSinceLastRefreshAndRestart();

            // Do not refresh if the UI is not visible. It will be refreshed later when the loaded event is called.
            if (IsVisible && Model.IsSolution)
            {
                var solutionModel = _detailModel as PackageSolutionDetailControlModel;
                if (solutionModel == null)
                {
                    return;
                }

                // get the list of projects
                var projects = solutionModel.Projects.Select(p => p.NuGetProject);
                Model.Context.Projects = projects;

                RefreshWhenNotExecutingAction(RefreshOperationSource.ProjectsChanged, timeSpan);
            }
            else
            {
                EmitRefreshEvent(timeSpan, RefreshOperationSource.ProjectsChanged, RefreshOperationStatus.NoOp);
            }
        }

        private void SolutionManager_ActionsExecuted(object sender, ActionsExecutedEventArgs e)
        {
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            // Do not refresh if the UI is not visible. It will be refreshed later when the loaded event is called.
            if (IsVisible)
            {
                if (Model.IsSolution)
                {
                    RefreshWhenNotExecutingAction(RefreshOperationSource.ActionsExecuted, timeSpan);
                }
                else
                {
                    // this is a project package manager, so there is one and only one project.
                    var project = Model.Context.Projects.First();
                    var projectName = NuGetProject.GetUniqueNameOrName(project);

                    // we need refresh when packages are installed into or uninstalled from the project
                    if (e.Actions.Any(action =>
                        NuGetProject.GetUniqueNameOrName(action.Project) == projectName))
                    {
                        RefreshWhenNotExecutingAction(RefreshOperationSource.ActionsExecuted, timeSpan);
                    }
                    else
                    {
                        EmitRefreshEvent(timeSpan, RefreshOperationSource.ActionsExecuted, RefreshOperationStatus.NotApplicable);
                    }
                }
            }
            else
            {
                EmitRefreshEvent(timeSpan, RefreshOperationSource.ActionsExecuted, RefreshOperationStatus.NoOp);
            }
        }

        private void SolutionManager_CacheUpdated(object sender, NuGetEventArgs<string> e)
        {
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            // Do not refresh if the UI is not visible. It will be refreshed later when the loaded event is called.
            if (IsVisible)
            {
                if (Model.IsSolution)
                {
                    RefreshWhenNotExecutingAction(RefreshOperationSource.CacheUpdated, timeSpan);
                }
                else
                {
                    // This is a project package manager, so there is one and only one project.
                    var project = Model.Context.Projects.First();

                    string projectFullName;

                    var projectContainsFullPath = project.TryGetMetadata(NuGetProjectMetadataKeys.FullPath, out projectFullName);

                    var eventProjectFullName = e.Arg;

                    // This ensures that we refresh the UI only if the event.project.FullName matches the NuGetProject.FullName.
                    // We also refresh the UI if projectFullPath is not present.
                    if (!projectContainsFullPath || projectFullName == eventProjectFullName)
                    {
                        RefreshWhenNotExecutingAction(RefreshOperationSource.CacheUpdated, timeSpan);
                    }
                    else
                    {
                        EmitRefreshEvent(timeSpan, RefreshOperationSource.CacheUpdated, RefreshOperationStatus.NotApplicable);
                    }
                }
            }
            else
            {
                EmitRefreshEvent(timeSpan, RefreshOperationSource.CacheUpdated, RefreshOperationStatus.NoOp);
            }
        }

        private void RefreshWhenNotExecutingAction(RefreshOperationSource source, TimeSpan timeSpanSinceLastRefresh)
        {
            // Only refresh if there is no executing action. Tell the operation execution to refresh when done otherwise.
            if (_isExecutingAction)
            {
                _isRefreshRequired = true;
                EmitRefreshEvent(timeSpanSinceLastRefresh, source, RefreshOperationStatus.NoOp);
            }
            else
            {
                Refresh();
                EmitRefreshEvent(timeSpanSinceLastRefresh, source, RefreshOperationStatus.Success);
            }
        }

        private void EmitRefreshEvent(TimeSpan timeSpan, RefreshOperationSource refreshOperationSource, RefreshOperationStatus status, bool isUIFiltering = false)
        {
            TelemetryActivity.EmitTelemetryEvent(
                                new PackageManagerUIRefreshEvent(
                                    _sessionGuid,
                                    Model.IsSolution,
                                    refreshOperationSource,
                                    status,
                                    _topPanel.Filter.ToString(),
                                    isUIFiltering,
                                    timeSpan));
        }

        private TimeSpan GetTimeSinceLastRefreshAndRestart()
        {
            TimeSpan elapsed;
            lock (_sinceLastRefresh)
            {
                elapsed = _sinceLastRefresh.Elapsed;
                _sinceLastRefresh.Restart();
            }
            return elapsed;
        }

        private void InitializeFilterList(UserSettings settings)
        {
            if (settings != null)
            {
                _topPanel.SelectFilter(settings.SelectedFilter);
            }
        }

        private void PackageManagerLoaded(object sender, RoutedEventArgs e)
        {
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            // Do not trigger a refresh if the browse tab is open and this is not the first load of the control.
            // The loaded event is triggered once all the data binding has occurred, which effectively means we'll just display what was loaded earlier and not trigger another search
            if (!(_loadedAndInitialized && _topPanel.Filter == ItemFilter.All))
            {
                _loadedAndInitialized = true;
                ResetTabDataLoadFlags();
                SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
                EmitRefreshEvent(timeSpan, RefreshOperationSource.PackageManagerLoaded, RefreshOperationStatus.Success);
            }
            else
            {
                EmitRefreshEvent(timeSpan, RefreshOperationSource.PackageManagerLoaded, RefreshOperationStatus.NoOp);
            }
            RefreshConsolidatablePackagesCount();
        }

        private void PackageManagerUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= PackageManagerUnloaded;
        }

        private static bool IsUILegalDisclaimerSuppressed()
        {
            return RegistrySettingUtility.GetBooleanSetting(Constants.SuppressUIDisclaimerRegistryName);
        }

        protected static DependencyBehavior? GetDependencyBehaviorFromConfig(
            Configuration.ISettings nugetSettings)
        {
            if (nugetSettings != null)
            {
                var configSection = nugetSettings.GetSection(ConfigurationConstants.Config);
                var dependencySetting = configSection?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, ConfigurationConstants.DependencyVersion);

                DependencyBehavior behavior;
                var success = Enum.TryParse(dependencySetting?.Value, ignoreCase: true, result: out behavior);
                if (success)
                {
                    return behavior;
                }
            }

            return null;
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

        public void ApplyShowDeprecatedFrameworkSetting(bool show)
        {
            _detailModel.Options.ShowDeprecatedFrameworkWindow = show;
        }

        private void ApplySettings(
            UserSettings settings,
            Configuration.ISettings nugetSettings)
        {
            var dependencySetting = GetDependencyBehaviorFromConfig(nugetSettings);
            if (settings == null)
            {
                // set depency behavior to the value from nugetSettings
                SetSelectedDepencyBehavior(dependencySetting ?? DependencyBehavior.Lowest);
                return;
            }

            _detailModel.Options.ShowPreviewWindow = settings.ShowPreviewWindow;
            _detailModel.Options.ShowDeprecatedFrameworkWindow = settings.ShowDeprecatedFrameworkWindow;
            _detailModel.Options.RemoveDependencies = settings.RemoveDependencies;
            _detailModel.Options.ForceRemove = settings.ForceRemove;
            _topPanel.CheckboxPrerelease.IsChecked = settings.IncludePrerelease;
            _packageDetail._optionsControl.IsExpanded = settings.OptionsExpanded;
            _packageDetail._solutionView.RestoreUserSettings(settings);

            SetSelectedDepencyBehavior(dependencySetting ?? settings.DependencyBehavior);

            var selectedFileConflictAction = _detailModel.Options.FileConflictActions.
                FirstOrDefault(a => a.Action == settings.FileConflictAction);
            if (selectedFileConflictAction != null)
            {
                _detailModel.Options.SelectedFileConflictAction = selectedFileConflictAction;
            }
        }

        private void Sources_PackageSourcesChanged(object sender, EventArgs e)
        {
            // Set _dontStartNewSearch to true to prevent a new search started in
            // _sourceRepoList_SelectionChanged(). This method will start the new
            // search when needed by itself.
            _dontStartNewSearch = true;
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            ResetTabDataLoadFlags();

            try
            {
                var prevSelectedItem = SelectedSource;
                PopulateSourceRepoList();

                // force a new search explicitly only if active source has changed
                if (prevSelectedItem == SelectedSource)
                {
                    EmitRefreshEvent(timeSpan, RefreshOperationSource.PackageSourcesChanged, RefreshOperationStatus.NotApplicable);
                }
                else
                {
                    SaveSettings();
                    SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
                    EmitRefreshEvent(timeSpan, RefreshOperationSource.PackageSourcesChanged, RefreshOperationStatus.Success);
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
            var settings = new UserSettings
            {
                SourceRepository = SelectedSource?.SourceName,
                ShowPreviewWindow = _detailModel.Options.ShowPreviewWindow,
                ShowDeprecatedFrameworkWindow = _detailModel.Options.ShowDeprecatedFrameworkWindow,
                RemoveDependencies = _detailModel.Options.RemoveDependencies,
                ForceRemove = _detailModel.Options.ForceRemove,
                DependencyBehavior = _detailModel.Options.SelectedDependencyBehavior.Behavior,
                FileConflictAction = _detailModel.Options.SelectedFileConflictAction.Action,
                IncludePrerelease = _topPanel.CheckboxPrerelease.IsChecked == true,
                SelectedFilter = _topPanel.Filter,
                OptionsExpanded = _packageDetail._optionsControl.IsExpanded
            };
            _packageDetail._solutionView.SaveSettings(settings);
            Model.Context.UserSettingsManager.AddSettings(GetSettingsKey(), settings);
        }

        private UserSettings LoadSettings()
        {
            var settings = Model.Context.UserSettingsManager.GetSettings(GetSettingsKey());

            if (PreviewWindow.IsDoNotShowPreviewWindowEnabled())
            {
                settings.ShowPreviewWindow = false;
            }

            if (DotnetDeprecatedPrompt.GetDoNotShowPromptState())
            {
                settings.ShowDeprecatedFrameworkWindow = false;
            }

            return settings;
        }

        private void AddRestoreBar()
        {
            if (Model.Context.PackageRestoreManager != null)
            {
                RestoreBar = new PackageRestoreBar(Model.Context.SolutionManager, Model.Context.PackageRestoreManager);
                DockPanel.SetDock(RestoreBar, Dock.Top);

                _root.Children.Insert(0, RestoreBar);

                Model.Context.PackageRestoreManager.PackagesMissingStatusChanged += packageRestoreManager_PackagesMissingStatusChanged;
            }
        }

        private void RemoveRestoreBar()
        {
            if (RestoreBar != null)
            {
                RestoreBar.CleanUp();
                Model.Context.PackageRestoreManager.PackagesMissingStatusChanged -= packageRestoreManager_PackagesMissingStatusChanged;
            }
        }

        private void AddRestartRequestBar(IVsShell4 vsRestarter)
        {
            if (Model.Context.PackageManager.DeleteOnRestartManager != null && vsRestarter != null)
            {
                _restartBar = new RestartRequestBar(Model.Context.PackageManager.DeleteOnRestartManager, vsRestarter);
                DockPanel.SetDock(_restartBar, Dock.Top);

                _root.Children.Insert(0, _restartBar);
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

        private void AddMigratorBar()
        {
            _migratorBar = new PRMigratorBar(Model);

            DockPanel.SetDock(_migratorBar, Dock.Top);

            _root.Children.Insert(0, _migratorBar);
        }

#pragma warning disable IDE1006 // Naming Styles
        private void packageRestoreManager_PackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
#pragma warning restore IDE1006 // Naming Styles
        {
            // make sure update happens on the UI thread.
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // TODO: PackageRestoreManager fires this event even when solution is closed.
                // Don't do anything if solution is closed.
                // Add MissingPackageStatus to keep previous packageMissing status to avoid unnecessarily refresh
                // only when package is missing last time and is not missing this time, we need to refresh
                if (!e.PackagesMissing && _missingPackageStatus)
                {
                    UpdateAfterPackagesMissingStatusChanged();
                }

                _missingPackageStatus = e.PackagesMissing;
            });
        }

        // Refresh the UI after packages are restored.
        // Note that the PackagesMissingStatusChanged event can be fired from a non-UI thread in one case:
        // the VsSolutionManager.Init() method, which is scheduled on the thread pool.
        private void UpdateAfterPackagesMissingStatusChanged()
        {
            VSThreadHelper.ThrowIfNotOnUIThread();
            var timeSinceLastRefresh = GetTimeSinceLastRefreshAndRestart();
            Refresh();
            EmitRefreshEvent(timeSinceLastRefresh, RefreshOperationSource.PackagesMissingStatusChanged, RefreshOperationStatus.Success);
            _packageDetail.Refresh();
        }

        private void SetTitle()
        {
            if (Model.IsSolution)
            {
                _topPanel.Title = Resx.Resources.Label_SolutionPackageManager;
            }
            else
            {
                var project = Model.Context.Projects.First();
                string projectName = null;
                if (!project.TryGetMetadata(NuGetProjectMetadataKeys.Name, out projectName))
                {
                    projectName = "unknown";
                }

                _topPanel.Title = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Resources.Label_PackageManager,
                    projectName);
            }
        }

        private void InitSourceRepoList(UserSettings settings)
        {
            // get active source name.
            string activeSourceName = null;

            // try saved user settings first.
            if (!string.IsNullOrEmpty(settings?.SourceRepository))
            {
                activeSourceName = settings.SourceRepository;
            }
            else
            {
                // no user settings found. Then use the active source from PackageSourceProvider.
                activeSourceName = Model.Context.SourceProvider.PackageSourceProvider.ActivePackageSourceName;
            }

            PopulateSourceRepoList(activeSourceName);
        }

        private IEnumerable<SourceRepository> GetEnabledSources()
        {
            return Model.Context.SourceProvider.GetRepositories().Where(s => s.PackageSource.IsEnabled);
        }

        private void PopulateSourceRepoList(string optionalSelectSourceName = null)
        {
            var selectedSourceName = optionalSelectSourceName ?? SelectedSource?.SourceName;

            // init source repo list
            _topPanel.SourceRepoList.Items.Clear();

            PackageSourceMoniker
                .PopulateList(Model.Context.SourceProvider)
                .ForEach(s => _topPanel.SourceRepoList.Items.Add(s));

            if (selectedSourceName != null)
            {
                SelectedSource = PackageSources
                    // if the old active source still exists. Keep it as the active source.
                    .FirstOrDefault(i => StringComparer.CurrentCultureIgnoreCase.Equals(i.SourceName, selectedSourceName))
                    // If the old active source does not exist any more. In this case,
                    // use the first (non-aggregate) enabled source as the active source.
                    ?? PackageSources.FirstOrDefault(psm => !psm.IsAggregateSource);
            }
            else
            {
                // use the first enabled source as the active source by default, but do not choose "All sources"!
                SelectedSource = PackageSources.FirstOrDefault(psm => !psm.IsAggregateSource);
            }
        }

        /// <summary>
        /// This method is called from several event handlers. So, consolidating the use of JTF.Run in this method
        /// </summary>
        private void SearchPackagesAndRefreshUpdateCount(bool useCacheForUpdates)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Set a new cancellation token source which will be used to cancel this task in case
                // new loading task starts or manager ui is closed while loading packages.
                var loadCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _loadCts, loadCts);
                oldCts?.Cancel();
                oldCts?.Dispose();

                await SearchPackagesAndRefreshUpdateCountAsync(
                    searchText: _windowSearchHost.SearchQuery.SearchString,
                    useCachedPackageMetadata: useCacheForUpdates,
                    pSearchCallback: null,
                    searchTask: null);
            })
            .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(SearchPackagesAndRefreshUpdateCount)));
        }

        // Enable if environment variable RecommendNuGetPackages is set to 1.
        public bool IsRecommenderFlightEnabled()
        {
            return _forceRecommender;
        }

        private async Task<(IPackageFeed mainFeed, IPackageFeed recommenderFeed)> GetPackageFeedsAsync(string searchText, PackageLoadContext loadContext)
        {
            // only make recommendations when
            //   the single source repository is nuget.org,
            //   the package manager was opened for a project, not a solution,
            //   this is the Browse tab,
            //   and the search text is an empty string
            _recommendPackages = false;
            if (loadContext.IsSolution == false
                && _topPanel.Filter == ItemFilter.All
                && searchText == string.Empty
                && loadContext.SourceRepositories.Count() == 1
                // also check if this is a PC-style project. We will not provide recommendations for PR-style
                // projects until we have a way to get dependent packages without negatively impacting perf.
                && Model.Context.Projects.First().ProjectStyle == ProjectModel.ProjectStyle.PackagesConfig
                && TelemetryUtility.IsNuGetOrg(loadContext.SourceRepositories.First().PackageSource))
            {
                _recommendPackages = true;
            }

            // Check for A/B experiment here. For control group, call CreatePackageFeedAsync with false instead of _recommendPackages
            var packageFeeds = (mainFeed: (IPackageFeed)null, recommenderFeed: (IPackageFeed)null);
            if (IsRecommenderFlightEnabled())
            {
                packageFeeds = await CreatePackageFeedAsync(loadContext, _topPanel.Filter, _uiLogger, _recommendPackages);
                _recommenderVersion = ((RecommenderPackageFeed)packageFeeds.recommenderFeed)?.VersionInfo;
            }
            else
            {
                packageFeeds = await CreatePackageFeedAsync(loadContext, _topPanel.Filter, _uiLogger, recommendPackages: false);
            }

            return packageFeeds;
        }

        /// <summary>
        /// This method is called from several event handlers. So, consolidating the use of JTF.Run in this method
        /// </summary>
        internal async Task SearchPackagesAndRefreshUpdateCountAsync(string searchText, bool useCachedPackageMetadata, IVsSearchCallback pSearchCallback, IVsSearchTask searchTask)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ItemFilter filterToRender = _topPanel.Filter;

            var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);

            if (useCachedPackageMetadata)
            {
                loadContext.CachedPackages = Model.CachedUpdates;
            }
            else // Invalidate cache
            {
                Model.CachedUpdates = null;
                FlagTabDataAsLoaded(filterToRender, isLoaded: false);
            }

            var packageFeeds = await GetPackageFeedsAsync(searchText, loadContext);

            try
            {
                var loader = new PackageItemLoader(
                    loadContext, packageFeeds.mainFeed, searchText, IncludePrerelease, packageFeeds.recommenderFeed);
                var loadingMessage = string.IsNullOrWhiteSpace(searchText)
                    ? Resx.Resources.Text_Loading
                    : string.Format(CultureInfo.CurrentCulture, Resx.Resources.Text_Searching, searchText);

                // Set a new cancellation token source which will be used to cancel this task in case
                // new loading task starts or manager ui is closed while loading packages.
                _loadCts = new CancellationTokenSource();

                // start SearchAsync task for initial loading of packages
                var searchResultTask = loader.SearchAsync(continuationToken: null, cancellationToken: _loadCts.Token);
                // this will wait for searchResultTask to complete instead of creating a new task
                await _packageList.LoadItemsAsync(loader, loadingMessage, _uiLogger, searchResultTask, _loadCts.Token);

                if (pSearchCallback != null && searchTask != null)
                {
                    var searchResult = await searchResultTask;
                    pSearchCallback.ReportComplete(searchTask, (uint)searchResult.RawItemsCount);
                }

                // When not using Cache, refresh all Counts.
                if (!useCachedPackageMetadata)
                {
                    RefreshInstalledAndUpdatesTabs();
                }

                FlagTabDataAsLoaded(filterToRender);

                // Loading Data on Installed tab should also consider the Data on Updates tab as loaded to indicate
                // UI filtering for Updates is ready.
                if (filterToRender == ItemFilter.Installed)
                {
                    FlagTabDataAsLoaded(ItemFilter.UpdatesAvailable);
                }
            }
            catch (OperationCanceledException)
            {
                // Invalidate cache.
                Model.CachedUpdates = null;
                FlagTabDataAsLoaded(filterToRender, isLoaded: false);
            }
        }

        /// <summary>
        /// Set a flag indicating this tab has been loaded for the first time since the control was loaded.
        /// Purpose is to identify cache availability and improve performance.
        /// When clearing this flag by <paramref name="isLoaded"/> to false, Installed and Updates will both be cleared
        /// since they are treated as one logical load.
        /// </summary>
        /// <param name="filterToCheck">Tab to mark as initially loaded. Currently supports Installed and Updates.</param>
        /// <param name="isLoaded">Set to false to reset the tab to its original state of not loaded.</param>
        private void FlagTabDataAsLoaded(ItemFilter filterToCheck, bool isLoaded = true)
        {
            switch (filterToCheck)
            {
                case ItemFilter.Installed:
                    _installedTabDataIsLoaded = isLoaded;
                    if (!isLoaded)
                    {
                        _updatesTabDataIsLoaded = false;
                    }
                    break;
                case ItemFilter.UpdatesAvailable:
                    _updatesTabDataIsLoaded = isLoaded;
                    if (!isLoaded)
                    {
                        _installedTabDataIsLoaded = false;
                    }
                    break;
                default:
                    break;
            }
        }

        private void ResetTabDataLoadFlags()
        {
            _installedTabDataIsLoaded = false;
            _updatesTabDataIsLoaded = false;
        }

        private void RefreshInstalledAndUpdatesTabs()
        {
            // clear existing caches
            Model.CachedUpdates = null;

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _topPanel.UpdateDeprecationStatusOnInstalledTab(installedDeprecatedPackagesCount: 0);
                _topPanel.UpdateCountOnUpdatesTab(count: 0);
                var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                var packageFeeds = await CreatePackageFeedAsync(loadContext, ItemFilter.UpdatesAvailable, _uiLogger, recommendPackages: false);
                var loader = new PackageItemLoader(
                    loadContext, packageFeeds.mainFeed, includePrerelease: IncludePrerelease, recommenderPackageFeed: packageFeeds.recommenderFeed);
                var metadataProvider = CreatePackageMetadataProvider(loadContext);

                // cancel previous refresh tabs task, if any
                // and start a new one.
                var refreshCts = new CancellationTokenSource();
                Interlocked.Exchange(ref _refreshCts, refreshCts)?.Cancel();

                // Update installed tab warning icon
                var installedDeprecatedPackagesCount = await GetInstalledDeprecatedPackagesCountAsync(
                    loadContext, metadataProvider, refreshCts.Token);

                var hasInstalledDeprecatedPackages = installedDeprecatedPackagesCount > 0;
                _topPanel.UpdateDeprecationStatusOnInstalledTab(installedDeprecatedPackagesCount);

                // Update updates tab count
                Model.CachedUpdates = new PackageSearchMetadataCache
                {
                    Packages = await loader.GetAllPackagesAsync(refreshCts.Token),
                    IncludePrerelease = IncludePrerelease
                };

                _topPanel.UpdateCountOnUpdatesTab(Model.CachedUpdates.Packages.Count);
            })
            .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(RefreshInstalledAndUpdatesTabs)));
        }

        private static async Task<int> GetInstalledDeprecatedPackagesCountAsync(PackageLoadContext loadContext, IPackageMetadataProvider metadataProvider, CancellationToken token)
        {
            // Switch off the UI thread before fetching installed packages and deprecation metadata.
            await TaskScheduler.Default;

            var installedPackages = await loadContext.GetInstalledPackagesAsync();

            var installedPackageDeprecationMetadata = await Task.WhenAll(
                installedPackages.Select(p => GetPackageDeprecationMetadataAsync(p, metadataProvider, token)));

            return installedPackageDeprecationMetadata.Count(d => d != null);
        }

        private static async Task<PackageDeprecationMetadata> GetPackageDeprecationMetadataAsync(
            PackageCollectionItem package, IPackageMetadataProvider metadataProvider, CancellationToken refreshToken)
        {
            var metadata = await metadataProvider.GetPackageMetadataAsync(package, includePrerelease: true, refreshToken);
            if (metadata == null)
            {
                return null;
            }

            return await metadata.GetDeprecationMetadataAsync();
        }

        private void RefreshConsolidatablePackagesCount()
        {
            if (Model.IsSolution)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _topPanel.UpdateCountOnConsolidateTab(count: 0);
                    var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                    var packageFeeds = await CreatePackageFeedAsync(loadContext, ItemFilter.Consolidate, _uiLogger, recommendPackages: false);
                    var loader = new PackageItemLoader(
                        loadContext, packageFeeds.mainFeed, includePrerelease: IncludePrerelease, recommenderPackageFeed: packageFeeds.recommenderFeed);

                    _topPanel.UpdateCountOnConsolidateTab(await loader.GetTotalCountAsync(maxCount: 100, CancellationToken.None));
                })
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(RefreshConsolidatablePackagesCount)));
            }
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            Model.UIController.LaunchNuGetOptionsDialog(OptionsPage.PackageSources);
        }

        private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory
                .RunAsync(UpdateDetailPaneAsync)
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(PackageList_SelectionChanged)));
        }

        /// <summary>
        /// Updates the detail pane based on the selected package
        /// </summary>
        internal async Task UpdateDetailPaneAsync()
        {
            var selectedPackage = _packageList.SelectedItem;
            var selectedIndex = _packageList.SelectedIndex;
            var recommendedCount = _packageList.PackageItems.Where(item => item.Recommended == true).Count();
            if (selectedPackage == null)
            {
                _packageDetail.Visibility = Visibility.Hidden;
            }
            else
            {
                _packageDetail.Visibility = Visibility.Visible;
                _packageDetail.DataContext = _detailModel;

                EmitSearchSelectionTelemetry(selectedPackage);

                await _detailModel.SetCurrentPackage(selectedPackage, _topPanel.Filter, () => _packageList.SelectedItem);
                _detailModel.SetCurrentSelectionInfo(selectedIndex, recommendedCount, _recommendPackages, _recommenderVersion);

                _packageDetail.ScrollToHome();

                var context = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                var metadataProvider = CreatePackageMetadataProvider(context);
                await _detailModel.LoadPackageMetadataAsync(metadataProvider, CancellationToken.None);
            }
        }

        private void EmitSearchSelectionTelemetry(PackageItemListViewModel selectedPackage)
        {
            var operationId = _packageList.OperationId;
            var selectedIndex = _packageList.SelectedIndex;
            var recommendedCount = _packageList.PackageItems.Where(item => item.Recommended == true).Count();
            if (_topPanel.Filter == ItemFilter.All
                && operationId.HasValue
                && selectedIndex >= 0)
            {
                TelemetryActivity.EmitTelemetryEvent(new SearchSelectionTelemetryEvent(
                    operationId.Value,
                    recommendedCount,
                    selectedIndex,
                    selectedPackage.Id,
                    selectedPackage.Version));
            }
        }

        private static async Task<(IPackageFeed mainFeed, IPackageFeed recommenderFeed)> CreatePackageFeedAsync(PackageLoadContext context, ItemFilter filter, INuGetUILogger uiLogger, bool recommendPackages)
        {
            var logger = new VisualStudioActivityLogger();
            var packageFeeds = (mainFeed: (IPackageFeed)null, recommenderFeed: (IPackageFeed)null);

            if (filter == ItemFilter.All && recommendPackages == false)
            {
                packageFeeds.mainFeed = new MultiSourcePackageFeed(
                    context.SourceRepositories,
                    uiLogger,
                    TelemetryActivity.NuGetTelemetryService);
                return packageFeeds;
            }

            var metadataProvider = CreatePackageMetadataProvider(context);
            var installedPackages = await context.GetInstalledPackagesAsync();

            if (filter == ItemFilter.All)
            {
                // if we get here, recommendPackages == true
                // for now, we are only making recommendations for PC-style projects, and for these the dependent packages are
                // already included in the installedPackages list. When we implement PR-style projects, we'll need to also pass
                // the dependent packages to RecommenderPackageFeed.
                var targetFrameworks = context.GetSupportedFrameworks();

                packageFeeds.mainFeed = new MultiSourcePackageFeed(
                    context.SourceRepositories,
                    uiLogger,
                    TelemetryActivity.NuGetTelemetryService);
                packageFeeds.recommenderFeed = new RecommenderPackageFeed(
                    context.SourceRepositories.First(),
                    installedPackages,
                    targetFrameworks,
                    metadataProvider,
                    logger);
                return packageFeeds;
            }

            if (filter == ItemFilter.Installed)
            {
                packageFeeds.mainFeed = new InstalledPackageFeed(installedPackages, metadataProvider, logger);
                return packageFeeds;
            }

            if (filter == ItemFilter.Consolidate)
            {
                packageFeeds.mainFeed = new ConsolidatePackageFeed(installedPackages, metadataProvider, logger);
                return packageFeeds;
            }

            // Search all / updates available cannot work without a source repo
            if (context.SourceRepositories == null)
            {
                return packageFeeds;
            }

            if (filter == ItemFilter.UpdatesAvailable)
            {
                packageFeeds.mainFeed = new UpdatePackageFeed(
                    installedPackages,
                    metadataProvider,
                    context.Projects,
                    context.CachedPackages,
                    logger);
                return packageFeeds;
            }

            throw new InvalidOperationException("Unsupported feed type");
        }

        private static IPackageMetadataProvider CreatePackageMetadataProvider(PackageLoadContext context)
        {
            var logger = new VisualStudioActivityLogger();

            return new MultiSourcePackageMetadataProvider(
                context.SourceRepositories,
                context.PackageManager?.PackagesFolderSourceRepository,
                context.PackageManager?.GlobalPackageFolderRepositories,
                logger);
        }

        private void SourceRepoList_SelectionChanged(object sender, EventArgs e)
        {
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            ResetTabDataLoadFlags();

            if (_dontStartNewSearch || !_initialized)
            {
                EmitRefreshEvent(timeSpan, RefreshOperationSource.SourceSelectionChanged, RefreshOperationStatus.NoOp);
                return;
            }

            if (SelectedSource != null)
            {
                _topPanel.SourceToolTip.Visibility = Visibility.Visible;
                _topPanel.SourceToolTip.DataContext = SelectedSource.GetTooltip();

                SaveSettings();
                SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
                EmitRefreshEvent(timeSpan, RefreshOperationSource.SourceSelectionChanged, RefreshOperationStatus.Success);
            }
        }

        private void Filter_SelectionChanged(object sender, FilterChangedEventArgs e)
        {
            if (_initialized)
            {
                var timeSpan = GetTimeSinceLastRefreshAndRestart();
                _packageList.ResetLoadingStatusIndicator();

                // Collapse the Update controls when the current tab is not "Updates".
                _packageList.CheckBoxesEnabled = _topPanel.Filter == ItemFilter.UpdatesAvailable;
                _packageList._updateButtonContainer.Visibility = _topPanel.Filter == ItemFilter.UpdatesAvailable ? Visibility.Visible : Visibility.Collapsed;

                // Set a new cancellation token source which will be used to cancel this task in case
                // new loading task starts or manager ui is closed while loading packages.
                var loadCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _loadCts, loadCts);
                oldCts?.Cancel();
                oldCts?.Dispose();

                var switchedFromInstalledOrUpdatesTab = e.PreviousFilter.HasValue &&
                    (e.PreviousFilter == ItemFilter.Installed || e.PreviousFilter == ItemFilter.UpdatesAvailable);
                var switchedToInstalledOrUpdatesTab = _topPanel.Filter == ItemFilter.UpdatesAvailable || _topPanel.Filter == ItemFilter.Installed;
                var installedAndUpdatesTabDataLoaded = _installedTabDataIsLoaded && _updatesTabDataIsLoaded;

                var isUiFiltering = switchedFromInstalledOrUpdatesTab && switchedToInstalledOrUpdatesTab && installedAndUpdatesTabDataLoaded;

                //Installed and Updates tabs don't need to be refreshed when switching between the two, if they're both loaded.
                if (isUiFiltering)
                {
                    //UI can apply filtering.
                    _packageList.FilterItems(_topPanel.Filter, _loadCts.Token);
                }
                else //Refresh tab from Cache.
                {
                    //If we came from a tab outside Installed/Updates, then they need to be Refreshed before UI filtering can take place.
                    if (!switchedFromInstalledOrUpdatesTab)
                    {
                        ResetTabDataLoadFlags();
                    }

                    SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: true);
                }
                EmitRefreshEvent(timeSpan, RefreshOperationSource.FilterSelectionChanged, RefreshOperationStatus.Success, isUiFiltering);

                _detailModel.OnFilterChanged(e.PreviousFilter, _topPanel.Filter);
            }
        }

        /// <summary>
        /// Refreshes the control after packages are installed or uninstalled.
        /// </summary>
        private void Refresh()
        {
            ResetTabDataLoadFlags();

            if (_topPanel.Filter != ItemFilter.All)
            {
                // refresh the whole package list
                SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
            }
            else
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var installedPackages = await PackageCollection.FromProjectsAsync(Model.Context.Projects,
                        CancellationToken.None);
                    _packageList.UpdatePackageStatus(installedPackages.ToArray());
                })
                .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(Refresh)));

                RefreshInstalledAndUpdatesTabs();
            }

            RefreshConsolidatablePackagesCount();

            _packageDetail?.Refresh();
        }

        private static PackageIdentity[] GetInstalledPackages(IEnumerable<NuGetProject> projects)
        {
            var installedPackages = NuGetUIThreadHelper.JoinableTaskFactory.Run(
                () => PackageCollection.FromProjectsAsync(projects, CancellationToken.None));

            return installedPackages.ToArray();
        }

        private void CheckboxPrerelease_CheckChanged(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }
            ResetTabDataLoadFlags();
            var timeSpan = GetTimeSinceLastRefreshAndRestart();
            RegistrySettingUtility.SetBooleanSetting(
                Constants.IncludePrereleaseRegistryName,
                _topPanel.CheckboxPrerelease.IsChecked == true);
            EmitRefreshEvent(timeSpan, RefreshOperationSource.CheckboxPrereleaseChanged, RefreshOperationStatus.Success);
            SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
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
            ResetTabDataLoadFlags();
            EmitRefreshEvent(GetTimeSinceLastRefreshAndRestart(), RefreshOperationSource.ClearSearch, RefreshOperationStatus.Success);
            SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: true);
        }

        public IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            var searchTask = new NuGetPackageManagerControlSearchTask(this, dwCookie, pSearchQuery, pSearchCallback);
            return searchTask;
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
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _windowSearchHost.Activate();
            })
            .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(FocusOnSearchBox_Executed)));
        }

        public void Search(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _windowSearchHost.Activate();
                _windowSearchHost.SearchAsync(new SearchQuery { SearchString = searchText });
            });
        }

        public void CleanUp()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _windowSearchHost.TerminateSearch();
            });

            RemoveRestoreBar();
            RemoveRestartBar();

            var solutionManager = Model.Context.SolutionManager;
            solutionManager.NuGetProjectAdded -= SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRemoved -= SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectUpdated -= SolutionManager_ProjectsUpdated;
            solutionManager.NuGetProjectRenamed -= SolutionManager_ProjectRenamed;
            solutionManager.ActionsExecuted -= SolutionManager_ActionsExecuted;
            solutionManager.AfterNuGetCacheUpdated -= SolutionManager_CacheUpdated;

            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesChanged -= Sources_PackageSourcesChanged;

            // make sure to cancel currently running load or refresh tasks
            _loadCts?.Cancel();
            _refreshCts?.Cancel();

            // make sure to dispose cancellation token source
            _loadCts?.Dispose();
            _refreshCts?.Dispose();

            _detailModel.CleanUp();
            _packageList.SelectionChanged -= PackageList_SelectionChanged;
        }

        private void SuppressDisclaimerChecked(object sender, RoutedEventArgs e)
        {
            _legalDisclaimer.Visibility = Visibility.Collapsed;
            RegistrySettingUtility.SetBooleanSetting(Constants.SuppressUIDisclaimerRegistryName, true);
        }

        /// <summary>
        /// This method is called after user clicks a button to execute an action, e.g. install a package.
        /// </summary>
        /// <param name="performAction">A function that returns the task that is performing the action
        /// thru UIActionEngine.</param>
        /// <param name="setOptions">A method that is called to set the action options,
        /// such as dependency behavior.</param>
        public void ExecuteAction(Func<Task> performAction, Action<NuGetUI> setOptions)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                IsEnabled = false;
                _isExecutingAction = true;

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
                        // Note that the task returned by performAction() will call something like
                        // UIActionEngine.PerformActionAsync(), which has to be called from a background thread.
                        // Thus, we need to use Task.Run() here.
                        await Task.Run(() => performAction());
                    }
                }
                finally
                {
                    //Invalidate cache.
                    Model.CachedUpdates = null;
                    ResetTabDataLoadFlags();

                    _actionCompleted?.Invoke(this, EventArgs.Empty);
                    NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageOperationEnd);
                    IsEnabled = true;
                    _isExecutingAction = false;
                    if (_isRefreshRequired)
                    {
                        var timeSinceLastRefresh = GetTimeSinceLastRefreshAndRestart();
                        Refresh();
                        EmitRefreshEvent(timeSinceLastRefresh, RefreshOperationSource.ExecuteAction, RefreshOperationStatus.Success);
                        _isRefreshRequired = false;
                    }
                }
            })
            .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(PackageManagerControl), nameof(ExecuteAction)));
        }

        private void ExecuteUninstallPackageCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var package = e.Parameter as PackageItemListViewModel;
            if (Model.IsSolution
                || package == null
                || package.Status == PackageStatus.NotInstalled)
            {
                return;
            }

            UninstallPackage(package.Id);
        }

        private void SetOptions(NuGetUI nugetUi, NuGetActionType actionType)
        {
            var options = _detailModel.Options;

            nugetUi.FileConflictAction = options.SelectedFileConflictAction.Action;
            nugetUi.DependencyBehavior = options.SelectedDependencyBehavior.Behavior;
            nugetUi.RemoveDependencies = options.RemoveDependencies;
            nugetUi.ForceRemove = options.ForceRemove;
            nugetUi.DisplayPreviewWindow = options.ShowPreviewWindow;
            nugetUi.DisplayDeprecatedFrameworkWindow = options.ShowDeprecatedFrameworkWindow;

            nugetUi.Projects = Model.Context.Projects;
            nugetUi.ProjectContext.ActionType = actionType;
        }

        private void ExecuteInstallPackageCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var package = e.Parameter as PackageItemListViewModel;
            if (package == null || Model.IsSolution)
            {
                return;
            }

            var versionToInstall = package.LatestVersion ?? package.Version;
            InstallPackage(package.Id, versionToInstall);
        }

        private void PackageList_UpdateButtonClicked(PackageItemListViewModel[] selectedPackages)
        {
            var packagesToUpdate = selectedPackages
                .Select(package => new PackageIdentity(package.Id, package.LatestVersion))
                .ToList();

            UpdatePackage(packagesToUpdate);
        }

        private void ExecuteRestartSearchCommand(object sender, ExecutedRoutedEventArgs e)
        {
            EmitRefreshEvent(GetTimeSinceLastRefreshAndRestart(), RefreshOperationSource.RestartSearchCommand, RefreshOperationStatus.Success);
            ResetTabDataLoadFlags();
            SearchPackagesAndRefreshUpdateCount(useCacheForUpdates: false);
            RefreshConsolidatablePackagesCount();
        }

        internal void InstallPackage(string packageId, NuGetVersion version)
        {
            var action = UserAction.CreateInstallAction(packageId, version);

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformActionAsync(
                        Model.UIController,
                        action,
                        CancellationToken.None);
                },

                nugetUi => SetOptions(nugetUi, NuGetActionType.Install));
        }

        internal void UninstallPackage(string packageId)
        {
            var action = UserAction.CreateUnInstallAction(packageId);

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformActionAsync(
                        Model.UIController,
                        action,
                        CancellationToken.None);
                },

                nugetUi => SetOptions(nugetUi, NuGetActionType.Uninstall));
        }

        internal void UpdatePackage(List<PackageIdentity> packages)
        {
            if (packages.Count == 0)
            {
                return;
            }

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformUpdateAsync(
                        Model.UIController,
                        packages,
                        CancellationToken.None);
                },
               nugetUi => SetOptions(nugetUi, NuGetActionType.Update));
        }

        private void UpgradeButton_Click(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var project = Model.Context.Projects.FirstOrDefault();
                Debug.Assert(project != null);
                await Model.Context.UIActionEngine.UpgradeNuGetProjectAsync(Model.UIController, project);
            })
            .FileAndForget(
                            TelemetryUtility.CreateFileAndForgetEventName(
                                nameof(PackageManagerControl),
                                nameof(UpgradeButton_Click)));
        }
    }
}
