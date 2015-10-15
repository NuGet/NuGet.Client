// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        private CancellationTokenSource _cts;
        private ILoader _loader;

        private int _startIndex;

        private const string LogEntrySource = "NuGet Package Manager";

        // The count of packages that are selected
        private int _selectedCount;

        public InfiniteScrollList()
        {
            InitializeComponent();

            CheckBoxesEnabled = false;
            _list.ItemsSource = Items;
            _startIndex = 0;
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

        public bool IsSolution
        {
            get; set;
        }

        public ObservableCollection<object> Items { get; } = new ObservableCollection<object>();

        // Load items using the specified loader
        public async Task LoadAsync(ILoader loader)
        {
            _loader = loader;
            _loadingStatusIndicator.LoadingMessage = _loader.LoadingMessage;

            var selectedItem = _list.SelectedItem as PackageItemListViewModel;

            foreach (var item in Items)
            {
                var package = item as PackageItemListViewModel;
                if (package != null)
                {
                    package.PropertyChanged -= Package_PropertyChanged;
                }
            }
            Items.Clear();

            Items.Add(_loadingStatusIndicator);
            _startIndex = 0;
            _selectedCount = 0;

            // now the package list
            await LoadAsync();

            if (selectedItem != null)
            {
                // select the the previously selected item if it still exists.
                foreach (var item in _list.Items)
                {
                    var package = item as PackageItemListViewModel;
                    if (package == null)
                    {
                        continue;
                    }

                    if (package.Id.Equals(selectedItem.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _list.SelectedItem = item;
                        break;
                    }
                }
            }

            UpdateCheckBoxStatus();
        }

        private Task LoadAsync()
        {
            if (_cts != null)
            {
                // There is another async loading process. Cancel it.
                _cts.Cancel();
            }

            _cts = new CancellationTokenSource();
            return LoadWorkAsync(_cts.Token);
        }

        private async Task LoadWorkAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _loadingStatusIndicator.Status = LoadingStatus.Loading;
            var currentLoader = _loader;
            try
            {
                // run Loader.LoadItems in background thread. Otherwise if the
                // source if V2, the UI can get blocked.
                var loadResult = await Task.Run(async () => await _loader.LoadItemsAsync(_startIndex, token));

                // multiple loads may occur at the same time
                if (!token.IsCancellationRequested
                    && currentLoader == _loader)
                {
                    UpdatePackageList(loadResult);

                    // select the first item if none was selected before
                    if (_list.SelectedIndex == -1 &&
                        Items.Count > 0 &&
                        Items[0] != _loadingStatusIndicator)
                    {
                        _list.SelectedIndex = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    // only display errors if this is still relevant
                    var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Resx.Resources.Text_ErrorOccurred,
                            ex.Message);

                    _loadingStatusIndicator.Status = LoadingStatus.ErrorOccured;
                    _loadingStatusIndicator.ErrorMessage = message;

                    // Write stack to activity log
                    Mvs.ActivityLog.LogError(LogEntrySource, ex.ToString());
                }
            }
        }

        private void UpdatePackageList(LoadResult loadResult)
        {
            // remove the loading status indicator if it's in the list
            if (Items.Count > 0 && Items[Items.Count - 1] == _loadingStatusIndicator)
            {
                Items.RemoveAt(Items.Count - 1);
            }

            // add newly loaded items
            foreach (var package in loadResult.Items)
            {
                package.PropertyChanged += Package_PropertyChanged;
                if (package.Selected)
                {
                    _selectedCount++;
                }

                Items.Add(package);
            }

            // update loading status indicator
            if (!loadResult.HasMoreItems)
            {
                if (Items.Count == 0)
                {
                    _loadingStatusIndicator.Status = LoadingStatus.NoItemsFound;
                }
                else
                {
                    _loadingStatusIndicator.Status = LoadingStatus.NoMoreItems;
                }
            }
            else
            {
                _startIndex = loadResult.NextStartIndex;
                _loadingStatusIndicator.Status = LoadingStatus.Ready;
            }

            if (_loadingStatusIndicator.Status != LoadingStatus.NoMoreItems)
            {
                Items.Add(_loadingStatusIndicator);
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

        public object SelectedItem
        {
            get { return _list.SelectedItem; }
        }

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
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    if (_loadingStatusIndicator.Status != LoadingStatus.Ready)
                    {
                        return;
                    }

                    var first = _scrollViewer.VerticalOffset;
                    var last = _scrollViewer.ViewportHeight + first;
                    if (last >= Items.Count)
                    {
                        await LoadAsync();
                    }
                });
        }

        private void RetryButtonClicked(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => { return LoadAsync(); });
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
            foreach (var item in _list.Items)
            {
                var package = item as PackageItemListViewModel;
                if (package != null && package.Selected)
                {
                    return true;
                }
            }

            return false;
        }

        private void _updateButton_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateButtonClicked != null && AnySelected())
            {
                UpdateButtonClicked(this, EventArgs.Empty);
            }
        }
    }
}