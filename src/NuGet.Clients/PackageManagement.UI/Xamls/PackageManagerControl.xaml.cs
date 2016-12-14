// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using Resx = NuGet.PackageManagement.UI;
using VSThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagerControl.xaml
    /// </summary>
    public partial class PackageManagerControl : UserControl, IVsWindowSearch
    {
        private readonly bool _initialized;

        private CancellationTokenSource _refreshCts;
        private CancellationTokenSource _loadCts;

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

        private readonly INuGetUILogger _uiLogger;

        public PackageManagerModel Model { get; }

        private PackageSourceMoniker SelectedSource
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

        private IEnumerable<PackageSourceMoniker> PackageSources => _topPanel.SourceRepoList.Items.OfType<PackageSourceMoniker>();

        internal IEnumerable<SourceRepository> ActiveSources => SelectedSource?.SourceRepositories ?? Enumerable.Empty<SourceRepository>();

        public bool IncludePrerelease => _topPanel.CheckboxPrerelease.IsChecked == true;

        public PackageManagerControl(
            PackageManagerModel model,
            Configuration.ISettings nugetSettings,
            IVsWindowSearchHostFactory searchFactory,
            IVsShell4 vsShell,
            INuGetUILogger uiLogger = null)
        {
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _uiLogger = uiLogger;
            Model = model;
            if (!Model.IsSolution)
            {
                _detailModel = new PackageDetailControlModel(Model.Context.Projects);
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
            var settings = LoadSettings();
            InitializeFilterList(settings);
            InitSourceRepoList(settings);
            ApplySettings(settings, nugetSettings);
            _initialized = true;

            // UI is initialized. Start the first search
            _packageList.CheckBoxesEnabled = _topPanel.Filter == ItemFilter.UpdatesAvailable;
            _packageList.IsSolution = this.Model.IsSolution;

            Loaded += (_, __) =>
            {
                SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
                RefreshConsolidatablePackagesCount();
            };

            // register with the UI controller
            var controller = model.UIController as NuGetUI;
            if (controller != null)
            {
                controller.PackageManagerControl = this;
            }

            var solutionManager = Model.Context.SolutionManager;
            solutionManager.NuGetProjectAdded += SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRemoved += SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRenamed += SolutionManager_ProjectRenamed;
            solutionManager.ActionsExecuted += SolutionManager_ActionsExecuted;

            Model.Context.SourceProvider.PackageSourceProvider.PackageSourcesChanged += Sources_PackageSourcesChanged;

            if (IsUILegalDisclaimerSuppressed())
            {
                _legalDisclaimer.Visibility = Visibility.Collapsed;
            }

            _missingPackageStatus = false;
        }

        private void SolutionManager_ProjectRenamed(object sender, NuGetProjectEventArgs e)
        {
            SolutionManager_ProjectsChanged(sender, e);
            if (!Model.IsSolution)
            {
                var currentNugetProject = Model.Context.Projects.First();
                var newNugetProject = e.NuGetProject;
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
            if (Model.IsSolution)
            {
                var solutionModel = _detailModel as PackageSolutionDetailControlModel;
                if (solutionModel == null)
                {
                    return;
                }

                // get the list of projects
                var projects = solutionModel.Projects.Select(p => p.NuGetProject);
                Model.Context.Projects = projects;

                // refresh UI
                Refresh();
            }
        }

        private void SolutionManager_ActionsExecuted(object sender, ActionsExecutedEventArgs e)
        {
            if (Model.IsSolution)
            {
                Refresh();
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
                    Refresh();
                }
            }
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

        protected static DependencyBehavior? GetDependencyBehaviorFromConfig(
            Configuration.ISettings nugetSettings)
        {
            if (nugetSettings != null)
            {
                var dependencySetting = nugetSettings.GetValue("config", "dependencyversion");
                DependencyBehavior behavior;
                var success = Enum.TryParse(dependencySetting, true, out behavior);
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
            try
            {
                var prevSelectedItem = SelectedSource;
                PopulateSourceRepoList();

                // force a new search explicitly if active source has changed
                if (prevSelectedItem != SelectedSource)
                {
                    SaveSettings();
                    SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
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
                _restoreBar = new PackageRestoreBar(Model.Context.SolutionManager, Model.Context.PackageRestoreManager);
                DockPanel.SetDock(_restoreBar, Dock.Top);

                _root.Children.Insert(0, _restoreBar);

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

        private void packageRestoreManager_PackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
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
        // the VsSolutionManager.Init() method, which is scheduled on the thread pool. So this
        // method needs to use _uiDispatcher.
        private void UpdateAfterPackagesMissingStatusChanged()
        {
            VSThreadHelper.ThrowIfNotOnUIThread();

            Refresh();
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
        private void SearchPackagesAndRefreshUpdateCount(string searchText, bool useCache)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);

                if (useCache)
                {
                    loadContext.CachedPackages = Model.CachedUpdates;
                };

                var packageFeed = await CreatePackageFeedAsync(loadContext, _topPanel.Filter, _uiLogger);

                var loader = new PackageItemLoader(
                    loadContext, packageFeed, searchText, IncludePrerelease);
                var loadingMessage = string.IsNullOrWhiteSpace(searchText)
                    ? Resx.Resources.Text_Loading
                    : string.Format(CultureInfo.CurrentCulture, Resx.Resources.Text_Searching, searchText);

                // Set a new cancellation token source which will be used to cancel this task in case
                // new loading task starts or manager ui is closed while loading packages.
                _loadCts = new CancellationTokenSource();

                // start SearchAsync task for initial loading of packages
                var searchResultTask = loader.SearchAsync(continuationToken: null, cancellationToken: _loadCts.Token);

                // this will wait for searchResultTask to complete instead of creating a new task
                _packageList.LoadItems(loader, loadingMessage, _uiLogger, searchResultTask, _loadCts.Token);

                // We only refresh update count, when we don't use cache so check it it's false
                if (!useCache)
                {
                    // clear existing caches
                    Model.CachedUpdates = null;

                    if (_topPanel.Filter.Equals(ItemFilter.UpdatesAvailable))
                    {
                        // it means selected tab is update itself, so just wait for searchAsyncTask to complete
                        // without making another call to loader to get all packages.
                        _topPanel._labelUpgradeAvailable.Count = 0;

                        var searchResult = await searchResultTask;
                        Model.CachedUpdates = new PackageSearchMetadataCache
                        {
                            Packages = searchResult.Items,
                            IncludePrerelease = IncludePrerelease
                        };

                        _topPanel._labelUpgradeAvailable.Count = Model.CachedUpdates.Packages.Count;
                    }
                    else
                    {
                        RefreshAvailableUpdatesCount();
                    }
                }
            });
        }

        private void RefreshAvailableUpdatesCount()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _topPanel._labelUpgradeAvailable.Count = 0;
                var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                var packageFeed = await CreatePackageFeedAsync(loadContext, ItemFilter.UpdatesAvailable, _uiLogger);
                var loader = new PackageItemLoader(
                    loadContext, packageFeed, includePrerelease: IncludePrerelease);

                // cancel previous refresh update count task, if any
                // and start a new one.
                var refreshCts = new CancellationTokenSource();
                Interlocked.Exchange(ref _refreshCts, refreshCts)?.Cancel();

                Model.CachedUpdates = new PackageSearchMetadataCache
                {
                    Packages = await loader.GetAllPackagesAsync(refreshCts.Token),
                    IncludePrerelease = IncludePrerelease
                };

                _topPanel._labelUpgradeAvailable.Count = Model.CachedUpdates.Packages.Count;
            });
        }

        private void RefreshConsolidatablePackagesCount()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _topPanel._labelConsolidate.Count = 0;
                var loadContext = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                var packageFeed = await CreatePackageFeedAsync(loadContext, ItemFilter.Consolidate, _uiLogger);
                var loader = new PackageItemLoader(
                    loadContext, packageFeed, includePrerelease: IncludePrerelease);

                _topPanel._labelConsolidate.Count = await loader.GetTotalCountAsync(100, CancellationToken.None);
            });
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            Model.UIController.LaunchNuGetOptionsDialog(OptionsPage.PackageSources);
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
            var selectedPackage = _packageList.SelectedItem;
            if (selectedPackage == null)
            {
                _packageDetail.Visibility = Visibility.Hidden;
                _packageDetail.DataContext = null;
            }
            else
            {
                _packageDetail.Visibility = Visibility.Visible;
                _packageDetail.DataContext = _detailModel;

                await _detailModel.SetCurrentPackage(selectedPackage, _topPanel.Filter);

                _packageDetail.ScrollToHome();

                var context = new PackageLoadContext(ActiveSources, Model.IsSolution, Model.Context);
                var metadataProvider = CreatePackageMetadataProvider(context);
                await _detailModel.LoadPackageMetadaAsync(metadataProvider, CancellationToken.None);
            }
        }

        private static async Task<IPackageFeed> CreatePackageFeedAsync(PackageLoadContext context, ItemFilter filter, INuGetUILogger uiLogger)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;

            var logger = new VisualStudioActivityLogger();

            if (filter == ItemFilter.All)
            {
                return new MultiSourcePackageFeed(context.SourceRepositories, uiLogger);
            }

            var metadataProvider = CreatePackageMetadataProvider(context);
            var installedPackages = await context.GetInstalledPackagesAsync();

            if (filter == ItemFilter.Installed)
            {
                return new InstalledPackageFeed(installedPackages, metadataProvider, logger);
            }

            if (filter == ItemFilter.Consolidate)
            {
                return new ConsolidatePackageFeed(installedPackages, metadataProvider, logger);
            }

            // Search all / updates available cannot work without a source repo
            if (context.SourceRepositories == null)
            {
                return null;
            }

            if (filter == ItemFilter.UpdatesAvailable)
            {
                return new UpdatePackageFeed(
                    installedPackages,
                    metadataProvider,
                    context.Projects,
                    context.CachedPackages,
                    logger);
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
            if (_dontStartNewSearch || !_initialized)
            {
                return;
            }

            if (SelectedSource != null)
            {
                _topPanel.SourceToolTip.Visibility = Visibility.Visible;
                _topPanel.SourceToolTip.DataContext = SelectedSource.GetTooltip();

                //Model.Context.SourceProvider.PackageSourceProvider.SaveActivePackageSource(ActiveSource.PackageSource);
                SaveSettings();
                SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
            }
        }

        private void Filter_SelectionChanged(object sender, FilterChangedEventArgs e)
        {
            if (_initialized)
            {
                _packageList.CheckBoxesEnabled = _topPanel.Filter == ItemFilter.UpdatesAvailable;
                SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: true);

                _detailModel.OnFilterChanged(e.PreviousFilter, _topPanel.Filter);
            }
        }

        /// <summary>
        /// Refreshes the control after packages are installed or uninstalled.
        /// </summary>
        private void Refresh()
        {
            if (_topPanel.Filter != ItemFilter.All)
            {
                // refresh the whole package list
                SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
            }
            else
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var installedPackages = await PackageCollection.FromProjectsAsync(Model.Context.Projects,
                        CancellationToken.None);
                    _packageList.UpdatePackageStatus(installedPackages.ToArray());
                });

                RefreshAvailableUpdatesCount();
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

        private void SearchControl_SearchStart(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: true);
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
            SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
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
            SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: true);
        }

        public IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            SearchPackagesAndRefreshUpdateCount(pSearchQuery.SearchString, useCache: true);
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

            var solutionManager = Model.Context.SolutionManager;
            solutionManager.NuGetProjectAdded -= SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRemoved -= SolutionManager_ProjectsChanged;
            solutionManager.NuGetProjectRenamed -= SolutionManager_ProjectRenamed;
            solutionManager.ActionsExecuted -= SolutionManager_ActionsExecuted;

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
                        // Note that the task returned by performAction() will call something like
                        // UIActionEngine.PerformActionAsync(), which has to be called from a background thread.
                        // Thus, we need to use Task.Run() here.
                        await Task.Run(() => performAction());
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
            var package = e.Parameter as PackageItemListViewModel;
            if (Model.IsSolution
                || package == null
                || package.Status == PackageStatus.NotInstalled)
            {
                return;
            }

            var action = UserAction.CreateUnInstallAction(package.Id);

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformActionAsync(
                        Model.UIController,
                        action,
                        this,
                        CancellationToken.None);
                },

                nugetUi => SetOptions(nugetUi, NuGetActionType.Uninstall));
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
            nugetUi.ProgressWindow.ActionType = actionType;
        }

        private void ExecuteInstallPackageCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var package = e.Parameter as PackageItemListViewModel;
            if (package == null || Model.IsSolution)
            {
                return;
            }

            var versionToInstall = package.LatestVersion ?? package.Version;
            var action = UserAction.CreateInstallAction(package.Id, versionToInstall);

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformActionAsync(
                        Model.UIController,
                        action,
                        this,
                        CancellationToken.None);
                },

                nugetUi => SetOptions(nugetUi, NuGetActionType.Install));
        }

        private void PackageList_UpdateButtonClicked(PackageItemListViewModel[] selectedPackages)
        {
            var packagesToUpdate = selectedPackages
                .Select(package => new PackageIdentity(package.Id, package.Version))
                .ToList();

            if (packagesToUpdate.Count == 0)
            {
                return;
            }

            ExecuteAction(
                () =>
                {
                    return Model.Context.UIActionEngine.PerformUpdateAsync(
                        Model.UIController,
                        packagesToUpdate,
                        this,
                        CancellationToken.None);
                },
               nugetUi => SetOptions(nugetUi, NuGetActionType.Update));
        }

        private void ExecuteRestartSearchCommand(object sender, ExecutedRoutedEventArgs e)
        {
            SearchPackagesAndRefreshUpdateCount(_windowSearchHost.SearchQuery.SearchString, useCache: false);
            RefreshConsolidatablePackagesCount();
        }
    }
}