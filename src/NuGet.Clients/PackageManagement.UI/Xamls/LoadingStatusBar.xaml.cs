using System;
using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
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

        #endregion ItemsLoaded DP

        public event EventHandler ShowMoreResultsClicked;

        public LoadingStatusBar()
        {
            InitializeComponent();
        }

        public void UpdateLoadingState(IItemLoaderState loaderState)
        {
            ViewModel?.UpdateModel(loaderState);
        }

        public void Reset(string loadingMessage)
        {
            DataContext = new LoadingStatusViewModel(loadingMessage);
        }

        private void ShowMoreResultsButton_Click(object sender, RoutedEventArgs e) =>
            ShowMoreResultsClicked?.Invoke(this, EventArgs.Empty);
    }
}