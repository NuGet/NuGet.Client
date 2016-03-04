using System.Globalization;
using System.Windows;
using Resx = NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    internal class LoadingStatusViewModel : DependencyObject
    {
        public LoadingStatusViewModel()
        {
            UpdateModel();
        }

        public LoadingStatusViewModel(string loadingMessage)
        {
            LoadingMessage = loadingMessage;
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

        #region LoadingMessage DP

        public string LoadingMessage
        {
            get { return (string)GetValue(LoadingMessageProperty); }
            set { SetValue(LoadingMessageProperty, value); }
        }

        public static readonly DependencyProperty LoadingMessageProperty = DependencyProperty.Register(
            nameof(LoadingMessage),
            typeof(string),
            typeof(LoadingStatusViewModel),
            new PropertyMetadata(string.Empty, OnLoadingMessagePropertyChanged));

        private static void OnLoadingMessagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoadingStatusViewModel)d).UpdateModel();
        }

        #endregion LoadingMessage DP

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

        #endregion LoadingStatus DP

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
                    return vm.ItemsLoaded == 0 ? vm.LoadingMessage : Resx.Resources.Text_SearchIncomplete;

                case LoadingStatus.Cancelled:
                    return Resx.Resources.Text_UserCanceled;

                case LoadingStatus.ErrorOccured:
                    return Resx.Resources.Text_SearchStopped;

                case LoadingStatus.NoItemsFound: // loading complete, no items found
                case LoadingStatus.NoMoreItems: // loading complete, no more items discovered beyond current page
                case LoadingStatus.Ready:
                    return Resx.Resources.Text_SearchCompleted;

                case LoadingStatus.Unknown:
                default:
                    return null;
            }
        }

        #endregion StatusMessage DP

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

        #endregion MessageLevel DP

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

        #endregion ItemsFound DP

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

        #endregion ItemsLoaded DP

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

        #endregion HasMoreItems DP

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

        #endregion MoreItemsLinkText DP
    }
}