// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NuGet.Common;
using NuGet.Packaging.Core;
using Mvs = Microsoft.VisualStudio.Shell;
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

        public event EventHandler UpdateButtonClicked;

        private CancellationTokenSource _loadCts;
        private IItemLoader<PackageItemListViewModel> _loader;

        private const string LogEntrySource = "NuGet Package Manager";

        // The count of packages that are selected
        private int _selectedCount;

        public InfiniteScrollList()
        {
            InitializeComponent();

            CheckBoxesEnabled = false;
            _list.ItemsSource = Items;
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

        public ObservableCollection<object> Items { get; } = new ObservableCollection<object>();
        public IEnumerable<PackageItemListViewModel> PackageItems => Items.OfType<PackageItemListViewModel>();
        public PackageItemListViewModel SelectedPackageItem => _list.SelectedItem as PackageItemListViewModel;

        // Load items using the specified loader
        internal async Task LoadAsync(IItemLoader<PackageItemListViewModel> loader, string loadingMessage)
        {
            _loader = loader;
            _loadingStatusIndicator.Reset(loadingMessage);

            var selectedPackageItem = SelectedPackageItem;
            ClearPackageList();

            _selectedCount = 0;

            // now the package list
            await LoadItemsAsync();

            UpdateSelectedItem(selectedPackageItem);
            UpdateCheckBoxStatus();
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

        private async Task LoadItemsAsync(bool refresh = false)
        {
            var loadCts = new CancellationTokenSource();
            // If there is another async loading process - cancel it.
            Interlocked.Exchange(ref _loadCts, loadCts)?.Cancel();

            try
            {
                await LoadItemsCoreAsync(refresh, loadCts.Token);
            }
            catch (OperationCanceledException) when (!loadCts.IsCancellationRequested)
            {
                // The user cancelled the login, but treat as a load error in UI
                // So the retry button and message is displayed
                // Do not log to the activity log, since it is not a NuGet error
                _loadingStatusIndicator.SetError(Resx.Resources.Text_UserCanceled);
            }
            catch (Exception ex) when (!loadCts.IsCancellationRequested)
            {
                loadCts.Cancel();

                // only display errors if this is still relevant
                var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Resx.Resources.Text_ErrorOccurred,
                        Environment.NewLine,
                        ExceptionUtilities.DisplayMessage(ex));
                _loadingStatusIndicator.SetError(new[] { Resx.Resources.Text_ErrorOccurred, message });

                // Write stack to activity log
                Mvs.ActivityLog.LogError(LogEntrySource, ex.ToString());
            }
        }

        private async Task LoadItemsCoreAsync(bool refresh, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            ShowLoadingStatus(LoadingStatus.Loading);

            var currentLoader = _loader;

            // run Loader.LoadItems in background thread. Otherwise if the
            // source if V2, the UI can get blocked.
            await Task.Run(() => InvokeLoaderAsync(currentLoader, refresh, token));

            // multiple loads may occur at the same time
            if (!token.IsCancellationRequested
                && currentLoader == _loader)
            {
                UpdatePackageList(currentLoader.GetCurrent().ToList(), refresh);
            }
        }

        private async Task InvokeLoaderAsync(IItemLoader<PackageItemListViewModel> currentLoader, bool refresh, CancellationToken token)
        {
            var progress = new Progress<IItemLoaderState>(
                s => HandleItemLoaderStateChange(currentLoader, s));

            // trigger loading
            await currentLoader.LoadNextAsync(progress, token);

            // run to completion
            while (currentLoader.State.LoadingStatus == LoadingStatus.Loading)
            {
                await currentLoader.UpdateStateAsync(progress, token);
            }
        }

        private void HandleItemLoaderStateChange(IItemLoader<PackageItemListViewModel> loader, IItemLoaderState state)
        {
            if (loader == _loader)
            {
                _loadingStatusIndicator.UpdateLoadingStatus(state);
            }
        }

        private void ShowLoadingStatus(LoadingStatus status)
        {
            _loadingStatusIndicator.Status = status;

            if (!_loadingStatusIndicator.IsVisible)
            {
                Items.Remove(_loadingStatusIndicator);
            }
            else if (!Items.Contains(_loadingStatusIndicator))
            {
                Items.Add(_loadingStatusIndicator);
            }
        }

        private void UpdatePackageList(List<PackageItemListViewModel> packages, bool refresh)
        {
            var selectedItem = SelectedPackageItem;

            // remove the loading status indicator if it's in the list
            Items.Remove(_loadingStatusIndicator);

            if (refresh)
            {
                ClearPackageList();
            }

            _selectedCount += packages.Count(p => p.Selected);

            // add newly loaded items
            packages
                .ForEach(p =>
                {
                    p.PropertyChanged += Package_PropertyChanged;
                    Items.Add(p);
                });

            if (_loadingStatusIndicator.IsVisible)
            {
                Items.Add(_loadingStatusIndicator);
            }

            UpdateSelectedItem(selectedItem);
        }

        private void ClearPackageList()
        {
            PackageItems
                .ToList()
                .ForEach(i => i.PropertyChanged -= Package_PropertyChanged);
            Items.Clear();
        }

        public void UpdatePackageStatus(PackageIdentity[] installedPackages)
        {
            // in this case, we only need to update PackageStatus of
            // existing items in the package list
            PackageItems
                .ToList()
                .ForEach(i => i.UpdatePackageStatus(installedPackages));
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
            if (_loadingStatusIndicator.Status == LoadingStatus.Ready)
            {
                var first = _scrollViewer.VerticalOffset;
                var last = _scrollViewer.ViewportHeight + first;
                if (_scrollViewer.ViewportHeight > 0 && last >= Items.Count)
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => LoadItemsAsync(refresh: false));
                }
            }
        }

        private void RetryButtonClicked(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => LoadItemsAsync(refresh: true));
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

        // Returns true if there are any selected packages
        private bool AnySelected()
        {
            return _list.Items.OfType<PackageItemListViewModel>().Any(i => i.Selected);
        }

        private void _updateButton_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateButtonClicked != null && AnySelected())
            {
                UpdateButtonClicked(this, EventArgs.Empty);
            }
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
    }
}