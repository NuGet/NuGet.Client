using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InfiniteScrollList.xaml
    /// </summary>
    public partial class InfiniteScrollList : UserControl
    {
        private ObservableCollection<object> _items;
        private LoadingStatusIndicator _loadingStatusIndicator;
        private ScrollViewer _scrollViewer;

        public event SelectionChangedEventHandler SelectionChanged;

        private CancellationTokenSource _cts;

        private int _startIndex;

        public static Style PackageItemStyle;
        public static Style LoadingStatusIndicatorStyle;

        public InfiniteScrollList()
        {
            InitializeComponent();

            PackageItemStyle = (Style)this.FindResource("packageItemStyle");
            LoadingStatusIndicatorStyle = (Style)this.FindResource("loadingStatusIndicatorStyle");

            if (!StandaloneSwitch.IsRunningStandalone)
            {
                // it's running inside VS. Load needed resources
                Brushes.Initialize();

                var setter = new Setter(ListBoxItem.TemplateProperty, this.FindResource("ListBoxItemTemplate"));
                PackageItemStyle.Setters.Add(setter);
            }

            _loadingStatusIndicator = new LoadingStatusIndicator();
            _items = new ObservableCollection<object>();
            _list.ItemsSource = _items;
            _startIndex = 0;
        }

        public ObservableCollection<object> Items
        {
            get
            {
                return _items;
            }
        }

        private ILoader _loader;

        public ILoader Loader
        {
            get
            {
                return _loader;
            }
            set
            {
                _loader = value;
                _loadingStatusIndicator.LoadingMessage = _loader.LoadingMessage;
                var _ = Reload();
            }
        }

        // Reload items starting with index 0
        public async Task Reload()
        {
            _items.Clear();
            _items.Add(_loadingStatusIndicator);
            _startIndex = 0;

            // we need to load installed packages so that the package status can be
            // calculated.
            await _loader.LoadInstalledPackagesAsync();

            // now reload the package list
            await Load();
        }

        private async Task Load()
        {
            if (_cts != null)
            {
                // There is another async loading process. Cancel it.
                _cts.Cancel();
            }

            _cts = new CancellationTokenSource();
            await LoadWork(_cts.Token);
        }

        private async Task LoadWork(CancellationToken token)
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
                // source if V2, the UI can get blocked a little bit.
                var r = await Task.Run(async () => await Loader.LoadItems(_startIndex, _cts.Token));

                // multiple loads may occur at the same time
                if (!token.IsCancellationRequested && currentLoader == _loader)
                {
                    UpdatePackageList(r);

                    // select the first item if none was selected before
                    if (_list.SelectedIndex == -1 && _items.Count > 1)
                    {
                        _list.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // only display errors if this is still relevant
                if (!token.IsCancellationRequested)
                {
                    var message = String.Format(
                            CultureInfo.CurrentCulture,
                            Resx.Resources.Text_ErrorOccurred,
                            ex);
                    _loadingStatusIndicator.Status = LoadingStatus.ErrorOccured;
                    _loadingStatusIndicator.ErrorMessage = message;
                }
            }
        }

        private void UpdatePackageList(LoadResult r)
        {
            // remove the loading status indicator if it's in the list
            if (_items[_items.Count - 1] == _loadingStatusIndicator)
            {
                _items.RemoveAt(_items.Count - 1);
            }

            // add newly loaded items
            foreach (var obj in r.Items)
            {
                _items.Add(obj);
            }

            // update loading status indicator
            if (!r.HasMoreItems)
            {
                if (_items.Count == 0)
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
                _startIndex = r.NextStartIndex;
                _loadingStatusIndicator.Status = LoadingStatus.Ready;
            }

            if (_loadingStatusIndicator.Status != LoadingStatus.NoMoreItems)
            {
                _items.Add(_loadingStatusIndicator);
            }
        }

        public object SelectedItem
        {
            get
            {
                return _list.SelectedItem;
            }
        }

        private void _list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is LoadingStatusIndicator)
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

        private void _list_Loaded(object sender, RoutedEventArgs e)
        {
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
            _scrollViewer.ScrollChanged += _scrollViewer_ScrollChanged;
        }

        private async void _scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_loadingStatusIndicator.Status != LoadingStatus.Ready)
            {
                return;
            }

            var first = _scrollViewer.VerticalOffset;
            var last = _scrollViewer.ViewportHeight + first;
            if (last >= _items.Count)
            {
                await Load();
            }
        }

        private async void RetryButtonClicked(object sender, RoutedEventArgs e)
        {
            await Load();
        }

        internal void SelectFirstItem()
        {
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }
    }

    public class LoadResult
    {
        public IEnumerable Items { get; set; }

        public bool HasMoreItems { get; set; }

        public int NextStartIndex { get; set; }
    }

    public interface ILoader
    {
        // The second value tells us whether there are more items to load
        Task<LoadResult> LoadItems(int startIndex, CancellationToken ct);

        string LoadingMessage { get; }

        Task LoadInstalledPackagesAsync();
    }

    public enum LoadingStatus
    {
        Ready,
        Loading,
        NoMoreItems,
        NoItemsFound,
        ErrorOccured
    }

    internal class LoadingStatusIndicator : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private LoadingStatus _status;

        private string _errorMessage;

        public LoadingStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
        }

        private string _loadingMessage;

        public string LoadingMessage
        {
            get
            {
                return _loadingMessage;
            }
            set
            {
                if (_loadingMessage != value)
                {
                    _loadingMessage = value;
                    OnPropertyChanged("LoadingMessage");
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged("ErrorMessage");
                }
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }
    }

    internal class InfiniteScrollListItemStyleSelector : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container)
        {
            if (item is LoadingStatusIndicator)
            {
                return InfiniteScrollList.LoadingStatusIndicatorStyle;
            }

            return InfiniteScrollList.PackageItemStyle;
        }
    }
}