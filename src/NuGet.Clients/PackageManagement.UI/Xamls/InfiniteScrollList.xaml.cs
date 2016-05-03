// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NuGet.Common;
using NuGet.Packaging.Core;
using Mvs = Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InfiniteScrollList.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001")]
    public partial class InfiniteScrollList : UserControl
    {
        private readonly LoadingStatusIndicator _loadingStatusIndicator = new LoadingStatusIndicator();
        private ScrollViewer _scrollViewer;

        public event SelectionChangedEventHandler SelectionChanged;

        public delegate void UpdateButtonCllickEventHandler(PackageItemListViewModel[] selectedPackages);
        public event UpdateButtonCllickEventHandler UpdateButtonClicked;

        private CancellationTokenSource _loadCts;
        private IItemLoader<PackageItemListViewModel> _loader;
        private INuGetUILogger _logger;

        private const string LogEntrySource = "NuGet Package Manager";

        // The count of packages that are selected
        private int _selectedCount;

        public InfiniteScrollList()
        {
            InitializeComponent();

            DataContext = Items;
            CheckBoxesEnabled = false;
        }

        // Indicates wether check boxes are enabled on packages
        private bool _checkBoxesEnabled;

        public bool CheckBoxesEnabled
        {
            get
            {
                return _checkBoxesEnabled;
            }
            set
            {
                _checkBoxesEnabled = value;

                if (!_checkBoxesEnabled)
                {
                    // the current tab is not "updates", so the container
                    // should become invisible.
                    _updateButtonContainer.Visibility = Visibility.Collapsed;
                }
            }
        }

        public bool IsSolution { get; set; }

        private readonly ObservableCollection<object> _items = new ObservableCollection<object>(); 

        public ObservableCollection<object> Items
        {
            get
            {
                Debug.Assert(Mvs.ThreadHelper.CheckAccess());
                return _items;
            }
        }

        public IEnumerable<PackageItemListViewModel> PackageItems => Items.OfType<PackageItemListViewModel>().ToArray();

        public PackageItemListViewModel SelectedPackageItem => _list.SelectedItem as PackageItemListViewModel;

        // Load items using the specified loader
        internal void LoadItems(IItemLoader<PackageItemListViewModel> loader, string loadingMessage, INuGetUILogger logger)
        {
            _loader = loader;
            _logger = logger;
            _loadingStatusIndicator.Reset(loadingMessage);
            _loadingStatusBar.Visibility = Visibility.Hidden;
            _loadingStatusBar.Reset(loadingMessage, loader.IsMultiSource);

            var selectedPackageItem = SelectedPackageItem;
            ClearPackageList();

            _selectedCount = 0;

            // triggers the package list loader
            LoadItems(selectedPackageItem);
        }

        private void UpdateSelectedItem(PackageItemListViewModel selectedItem)
        {
            if (selectedItem != null)
            {
                // select the the previously selected item if it still exists.
                selectedItem = PackageItems
                    .FirstOrDefault(item => item.Id.Equals(selectedItem.Id, StringComparison.OrdinalIgnoreCase));
            }

            // select the first item if none was selected before
            _list.SelectedItem = selectedItem ?? PackageItems.FirstOrDefault();
        }

        private void LoadItems(PackageItemListViewModel selectedPackageItem)
        {
            var loadCts = new CancellationTokenSource();
            // If there is another async loading process - cancel it.
            Interlocked.Exchange(ref _loadCts, loadCts)?.Cancel();

            var currentLoader = _loader;

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;

                try
                {
                    await LoadItemsCoreAsync(currentLoader, loadCts.Token);

                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (selectedPackageItem != null)
                    {
                        UpdateSelectedItem(selectedPackageItem);
                    }
                }
                catch (OperationCanceledException) when (!loadCts.IsCancellationRequested)
                {
                    loadCts.Cancel();
                    currentLoader.Reset();

                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // The user cancelled the login, but treat as a load error in UI
                    // So the retry button and message is displayed
                    // Do not log to the activity log, since it is not a NuGet error
                    _logger.Log(ProjectManagement.MessageLevel.Error, Resx.Resources.Text_UserCanceled);

                    _loadingStatusIndicator.SetError(Resx.Resources.Text_UserCanceled);

                    _loadingStatusBar.SetCancelled();
                    _loadingStatusBar.Visibility = Visibility.Visible;
                }
                catch (Exception ex) when (!loadCts.IsCancellationRequested)
                {
                    loadCts.Cancel();
                    currentLoader.Reset();

                    // Write stack to activity log
                    Mvs.ActivityLog.LogError(LogEntrySource, ex.ToString());

                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var errorMessage = ExceptionUtilities.DisplayMessage(ex);
                    _logger.Log(ProjectManagement.MessageLevel.Error, errorMessage);

                    _loadingStatusIndicator.SetError(errorMessage);

                    _loadingStatusBar.SetError();
                    _loadingStatusBar.Visibility = Visibility.Visible;
                }

                UpdateCheckBoxStatus();
            });
        }

        private async Task LoadItemsCoreAsync(IItemLoader<PackageItemListViewModel> currentLoader, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var loadedItems = await LoadNextPageAsync(currentLoader, token);
            token.ThrowIfCancellationRequested();

            await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

               // multiple loads may occur at the same time as a result of multiple instances,
               // makes sure we update using the relevant one.
                if (currentLoader == _loader)
                {
                    _loadingStatusBar.ItemsLoaded = currentLoader.State.ItemsCount;
                    UpdatePackageList(loadedItems, refresh: false);
                }
                
            });

            token.ThrowIfCancellationRequested();

            // keep waiting till completion
            await WaitForCompletionAsync(currentLoader, token);

            token.ThrowIfCancellationRequested();
        }

        private async Task<IEnumerable<PackageItemListViewModel>> LoadNextPageAsync(IItemLoader<PackageItemListViewModel> currentLoader, CancellationToken token)
        {
            var progress = new Progress<IItemLoaderState>(
                s => HandleItemLoaderStateChange(currentLoader, s));

            // trigger loading
            await currentLoader.LoadNextAsync(progress, token);

            // run till first results are ready
            for (var state = currentLoader.State;
                state.LoadingStatus == LoadingStatus.Loading && state.ItemsCount == 0;
                state = currentLoader.State)
            {
                token.ThrowIfCancellationRequested();
                await currentLoader.UpdateStateAsync(progress, token);
            }

            return currentLoader.GetCurrent();
        }

        private async Task WaitForCompletionAsync(IItemLoader<PackageItemListViewModel> currentLoader, CancellationToken token)
        {
            var progress = new Progress<IItemLoaderState>(
                s => HandleItemLoaderStateChange(currentLoader, s));

            // run to completion
            while (currentLoader.State.LoadingStatus == LoadingStatus.Loading)
            {
                token.ThrowIfCancellationRequested();
                await currentLoader.UpdateStateAsync(progress, token);
            }
        }

        private void HandleItemLoaderStateChange(IItemLoader<PackageItemListViewModel> loader, IItemLoaderState state)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (loader == _loader)
                {
                    _loadingStatusBar.UpdateLoadingState(state);

                    // decide when to show status bar
                    var desiredVisibility = EvaluateStatusBarVisibility(loader, state);

                    if (_loadingStatusBar.Visibility != Visibility.Visible 
                        && desiredVisibility == Visibility.Visible)
                    {
                        _loadingStatusBar.Visibility = desiredVisibility;
                    }

                    _loadingStatusIndicator.Status = state.LoadingStatus;

                    if (!Items.Contains(_loadingStatusIndicator))
                    {
                        Items.Add(_loadingStatusIndicator);
                    }
                }
            });
        }

        private Visibility EvaluateStatusBarVisibility(IItemLoader<PackageItemListViewModel> loader, IItemLoaderState state)
        {
            var statusBarVisibility = Visibility.Hidden;

            if (state.LoadingStatus == LoadingStatus.Cancelled
                || state.LoadingStatus == LoadingStatus.ErrorOccurred)
            {
                statusBarVisibility = Visibility.Visible;
            }

            if (loader.IsMultiSource)
            {
                bool hasMore = _loadingStatusBar.ItemsLoaded != 0 && state.ItemsCount > _loadingStatusBar.ItemsLoaded;
                if (hasMore)
                {
                    statusBarVisibility = Visibility.Visible;
                }

                if (state.LoadingStatus == LoadingStatus.Loading && state.ItemsCount > 0)
                {
                    statusBarVisibility = Visibility.Visible;
                }
            }

            return statusBarVisibility;
        }

        private void UpdatePackageList(IEnumerable<PackageItemListViewModel> packages, bool refresh)
        {
            // remove the loading status indicator if it's in the list
            Items.Remove(_loadingStatusIndicator);

            if (refresh)
            {
                ClearPackageList();
            }

            // add newly loaded items
            foreach (var package in packages)
            {
                package.PropertyChanged += Package_PropertyChanged;
                Items.Add(package);
                _selectedCount = package.Selected ? _selectedCount + 1 : _selectedCount;
            }

            Items.Add(_loadingStatusIndicator);
        }

        private void ClearPackageList()
        {
            foreach (var package in PackageItems)
            {
                package.PropertyChanged -= Package_PropertyChanged;
            }

            Items.Clear();
            _loadingStatusBar.ItemsLoaded = 0;
        }

        public void UpdatePackageStatus(PackageIdentity[] installedPackages)
        {
            // in this case, we only need to update PackageStatus of
            // existing items in the package list
            foreach (var package in PackageItems)
            {
                package.UpdatePackageStatus(installedPackages);
            }
        }

        private void Package_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var package = sender as PackageItemListViewModel;
            if (e.PropertyName == nameof(package.Selected))
            {
                if (package.Selected)
                {
                    _selectedCount++;
                }
                else
                {
                    _selectedCount--;
                }

                UpdateCheckBoxStatus();
            }
        }

        // Update the status of the _selectAllPackages check box and the Update button.
        private void UpdateCheckBoxStatus()
        {
            if (!CheckBoxesEnabled)
            {
                // the current tab is not "updates"
                _updateButtonContainer.Visibility = Visibility.Collapsed;
                return;
            }

            int packageCount;
            if (Items.Count == 0)
            {
                packageCount = 0;
            }
            else
            {
                if (Items[Items.Count - 1] == _loadingStatusIndicator)
                {
                    packageCount = Items.Count - 1;
                }
                else
                {
                    packageCount = Items.Count;
                }
            }

            // update the container's visibility
            _updateButtonContainer.Visibility =
                packageCount > 0 ?
                Visibility.Visible :
                Visibility.Collapsed;

            if (_selectedCount == 0)
            {
                _selectAllPackages.IsChecked = false;
                _updateButton.IsEnabled = false;
            }
            else if (_selectedCount < packageCount)
            {
                _selectAllPackages.IsChecked = null;
                _updateButton.IsEnabled = true;
            }
            else
            {
                _selectAllPackages.IsChecked = true;
                _updateButton.IsEnabled = true;
            }
        }

        public PackageItemListViewModel SelectedItem => _list.SelectedItem as PackageItemListViewModel;

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0
                && e.AddedItems[0] is LoadingStatusIndicator)
            {
                // make the loading object not selectable
                if (e.RemovedItems.Count > 0)
                {
                    _list.SelectedItem = e.RemovedItems[0];
                }
                else
                {
                    _list.SelectedIndex = -1;
                }
            }
            else
            {
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, e);
                }
            }
        }

        private void List_Loaded(object sender, RoutedEventArgs e)
        {
            _list.Loaded -= List_Loaded;

            var c = VisualTreeHelper.GetChild(_list, 0) as Border;
            if (c == null)
            {
                return;
            }

            c.Padding = new Thickness(0);
            _scrollViewer = VisualTreeHelper.GetChild(c, 0) as ScrollViewer;
            if (_scrollViewer == null)
            {
                return;
            }

            _scrollViewer.Padding = new Thickness(0);
            _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_loader?.State.LoadingStatus == LoadingStatus.Ready)
            {
                var first = _scrollViewer.VerticalOffset;
                var last = _scrollViewer.ViewportHeight + first;
                if (_scrollViewer.ViewportHeight > 0 && last >= Items.Count)
                {
                    LoadItems(selectedPackageItem: null);
                }
            }
        }

        private void SelectAllPackagesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _list.Items)
            {
                var package = item as PackageItemListViewModel;

                // note that item could be the loading indicator, thus we need to check
                // for null here.
                if (package != null)
                {
                    package.Selected = true;
                }
            }
        }

        private void SelectAllPackagesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _list.Items)
            {
                var package = item as PackageItemListViewModel;
                if (package != null)
                {
                    package.Selected = false;
                }
            }
        }

        private void _updateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPackages = PackageItems.Where(p => p.Selected).ToArray();
            UpdateButtonClicked(selectedPackages);
        }

        private void List_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // toggle the selection state when user presses the space bar
            var package = _list.SelectedItem as PackageItemListViewModel;
            if (package != null && e.Key == Key.Space)
            {
                package.Selected = !package.Selected;
                e.Handled = true;
            }
        }

        private void _loadingStatusBar_ShowMoreResultsClick(object sender, RoutedEventArgs e)
        {
            var packageItems = _loader?.GetCurrent() ?? Enumerable.Empty<PackageItemListViewModel>();
            UpdatePackageList(packageItems, refresh: true);
            _loadingStatusBar.ItemsLoaded = _loader?.State.ItemsCount ?? 0;

            var desiredVisibility = EvaluateStatusBarVisibility(_loader, _loader.State);
            if (_loadingStatusBar.Visibility != desiredVisibility)
            {
                _loadingStatusBar.Visibility = desiredVisibility;
            }
        }

        private void _loadingStatusBar_DismissClick(object sender, RoutedEventArgs e)
        {
            _loadingStatusBar.Visibility = Visibility.Hidden;
        }
    }
}