// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents a list of packages containing <see cref="PackageItemViewModel"/> objects.
    /// </summary>
    public class PackageListViewModel : ViewModelBase
    {
        public ObservableCollection<PackageItemViewModel> Collection { get; private set; }
        public LoadingStatusIndicator LoadingStatusIndicator { get; private set; }
        public bool HasStatusToDisplay
        {
            get
            {
                return LoadingStatusIndicator.HasStatusToDisplay;
            }
        }
        public bool ShowNoPackagesFound
        {
            get
            {
                return LoadingStatusIndicator.Status == LoadingStatus.NoItemsFound;
            }
        }

        public bool IsRunning
        {
            get
            {
                return LoadingStatusIndicator.Status == LoadingStatus.Loading;
            }
        }

        public ICollectionView CollectionView
        {
            get
            {
                return CollectionViewSource.GetDefaultView(Collection);
            }
        }

        private PackageListViewModel()
        { }

        public PackageListViewModel(ObservableCollection<PackageItemViewModel> collection)
        {
            Collection = collection;
            LoadingStatusIndicator = new LoadingStatusIndicator();
            LoadingStatusIndicator.PropertyChanged += LoadingStatusIndicator_PropertyChanged;
        }

        private void LoadingStatusIndicator_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(HasStatusToDisplay));
            RaisePropertyChanged(nameof(ShowNoPackagesFound));

            //RaisePropertyChanged(nameof(LoadingStatusIndicator));
        }
    }
}
