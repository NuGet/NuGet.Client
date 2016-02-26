using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    internal class LoadingStatusViewModel : DependencyObject
    {
        private readonly string _loadingMessage;

        public LoadingStatusViewModel()
        {
            _loadingMessage = string.Empty;
        }
        public LoadingStatusViewModel(string loadingMessage)
        {
            _loadingMessage = loadingMessage;
            UpdateModel();
        }

        public void UpdateModel(IItemLoaderState loaderState)
        {
            LoadingStatus = loaderState.LoadingStatus;
            ItemsFound = loaderState.ItemsCount;
        }

        private void UpdateModel()
        {
            // order is important!
            CoerceValue(HasMoreItemsProperty);
            CoerceValue(StatusMessageProperty);
            CoerceValue(MessageLevelProperty);
            CoerceValue(MoreItemsLinkTextProperty);
        }

        #region LoadingStatus DP

        public LoadingStatus LoadingStatus
        {
            get { return (LoadingStatus)GetValue(LoadingStatusProperty); }
            set { SetValue(LoadingStatusProperty, value); }
        }

        public static readonly DependencyProperty LoadingStatusProperty = DependencyProperty.Register(
            nameof(LoadingStatus),
            typeof(LoadingStatus),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(LoadingStatus.Unknown, OnLoadingStatusPropertyChanged));

        private static void OnLoadingStatusPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingStatusViewModel)d).UpdateModel();
        }

        #endregion

        #region StatusMessage DP

        public string StatusMessage
        {
            get { return ((string)GetValue(StatusMessageProperty)); }
            private set { SetValue(StatusMessageProperty, value); }
        }

        public static readonly DependencyProperty StatusMessageProperty = DependencyProperty.RegisterReadOnly(
            nameof(StatusMessage),
            typeof(string),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(string.Empty, null, OnCoerceStatusMessage))
            .DependencyProperty;

        private static object OnCoerceStatusMessage(DependencyObject d, object baseValue)
        {
            var vm = (LoadingStatusViewModel)d;
            switch (vm.LoadingStatus)
            {
                case LoadingStatus.Loading:
                    return vm.ItemsLoaded == 0 ? vm._loadingMessage : Resx.Resources.Text_SearchIncomplete;
                case LoadingStatus.Cancelled:
                    return Resx.Resources.Text_UserCanceled;
                case LoadingStatus.ErrorOccured:
                    return Resx.Resources.Text_SearchStopped;
                case LoadingStatus.NoMoreItems: // loading complete, no more items discovered beyond current page
                case LoadingStatus.Ready:
                    return Resx.Resources.Text_SearchCompleted;
                case LoadingStatus.NoItemsFound: // loading complete, no items found
                    return Resx.Resources.Text_NoItemsFound;
                case LoadingStatus.Unknown:
                default:
                    return null;
            }
        }

        #endregion

        #region MessageLevel DP

        public MessageLevel MessageLevel
        {
            get { return ((MessageLevel)GetValue(MessageLevelProperty)); }
            private set { SetValue(MessageLevelProperty, value); }
        }

        public static readonly DependencyProperty MessageLevelProperty = DependencyProperty.RegisterReadOnly(
            nameof(MessageLevel),
            typeof(MessageLevel),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(MessageLevel.Info, null, OnCoerceMessageLevel))
            .DependencyProperty;

        private static object OnCoerceMessageLevel(DependencyObject d, object baseValue)
        {
            var vm = (LoadingStatusViewModel)d;
            switch (vm.LoadingStatus)
            {
                case LoadingStatus.Loading:
                    return vm.ItemsFound == 0 ? MessageLevel.Info : MessageLevel.Warning;
                case LoadingStatus.Cancelled:
                case LoadingStatus.ErrorOccured:
                    return MessageLevel.Error;
                case LoadingStatus.Ready:
                    return !vm.HasMoreItems ? MessageLevel.Info : MessageLevel.Warning;
                case LoadingStatus.NoItemsFound:
                case LoadingStatus.NoMoreItems:
                case LoadingStatus.Unknown:
                default:
                    return MessageLevel.Info;
            }
        }

        #endregion

        #region ItemsFound DP

        public int ItemsFound
        {
            get { return (int)GetValue(ItemsFoundProperty); }
            set { SetValue(ItemsFoundProperty, value); }
        }

        public static readonly DependencyProperty ItemsFoundProperty = DependencyProperty.Register(
            nameof(ItemsFound),
            typeof(int),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(0, OnItemsFoundPropertyChanged));

        private static void OnItemsFoundPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingStatusViewModel)d).UpdateModel();
        }

        #endregion

        #region ItemsLoaded DP

        public int ItemsLoaded
        {
            get { return (int)GetValue(ItemsLoadedProperty); }
            set { SetValue(ItemsLoadedProperty, value); }
        }

        public static readonly DependencyProperty ItemsLoadedProperty = DependencyProperty.Register(
            nameof(ItemsLoaded),
            typeof(int),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(0, OnItemsLoadedPropertyChanged));

        private static void OnItemsLoadedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingStatusViewModel)d).UpdateModel();
        }

        #endregion

        #region HasMoreItems DP

        public bool HasMoreItems
        {
            get { return ((bool)GetValue(HasMoreItemsProperty)); }
            private set { SetValue(HasMoreItemsProperty, value); }
        }

        public static readonly DependencyProperty HasMoreItemsProperty = DependencyProperty.RegisterReadOnly(
            nameof(HasMoreItems),
            typeof(bool),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(false, null, OnCoerceHasMoreItems))
            .DependencyProperty;

        private static object OnCoerceHasMoreItems(DependencyObject d, object baseValue)
        {
            var vm = (LoadingStatusViewModel)d;
            return vm.ItemsFound > vm.ItemsLoaded;
        }

        #endregion

        #region MoreItemsLinkText DP

        public string MoreItemsLinkText
        {
            get { return ((string)GetValue(MoreItemsLinkTextProperty)); }
            private set { SetValue(MoreItemsLinkTextProperty, value); }
        }

        public static readonly DependencyProperty MoreItemsLinkTextProperty = DependencyProperty.RegisterReadOnly(
            nameof(MoreItemsLinkText),
            typeof(string),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(string.Empty, null, OnCoerceMoreItemsLinkText))
            .DependencyProperty;

        private static object OnCoerceMoreItemsLinkText(DependencyObject d, object baseValue)
        {
            var vm = (LoadingStatusViewModel)d;
            return string.Format(
                CultureInfo.CurrentCulture,
                Resx.Resources.Button_ShowMoreResults,
                vm.ItemsFound - vm.ItemsLoaded);
        }

        #endregion
    }

    /// <summary>
    /// Interaction logic for LoadingStatusBar.xaml
    /// </summary>
    internal partial class LoadingStatusBar : UserControl
    {
        public LoadingStatusViewModel ViewModel => DataContext as LoadingStatusViewModel;

        #region ItemsLoaded DP

        public int ItemsLoaded
        {
            get { return (int)GetValue(ItemsLoadedProperty); }
            set { SetValue(ItemsLoadedProperty, value); }
        }

        public static readonly DependencyProperty ItemsLoadedProperty = DependencyProperty.Register(
            nameof(ItemsLoaded),
            typeof(int),
            typeof(LoadingStatusBar),
            new PropertyMetadata(0, OnItemsLoadedPropertyChanged));

        private static void OnItemsLoadedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingStatusBar)d).ViewModel.ItemsLoaded = (int)e.NewValue;
        }

        #endregion

        public event EventHandler ShowMoreResultsClicked;
        public event EventHandler RestartSearchClicked;
        public event EventHandler ShowErrorsClicked;

        public LoadingStatusBar()
        {
            InitializeComponent();
        }

        public void UpdateLoadingState(IItemLoaderState loaderState)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ViewModel?.UpdateModel(loaderState);
            });
        }

        public void Reset(string loadingMessage)
        {
            DataContext = new LoadingStatusViewModel(loadingMessage);
        }

        private void ShowMoreResultsButton_Click(object sender, RoutedEventArgs e) =>
            ShowMoreResultsClicked?.Invoke(this, EventArgs.Empty);

        private void RestartSearchButton_Click(object sender, RoutedEventArgs e) =>
            RestartSearchClicked?.Invoke(this, EventArgs.Empty);

        private void ShowErrorsButton_Click(object sender, RoutedEventArgs e) =>
            ShowErrorsClicked?.Invoke(this, EventArgs.Empty);
    }
}
