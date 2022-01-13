// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;

namespace NuGet.PackageManagement.UI
{
    public class PackageManagerViewModel : ViewModelBase
    {
        private ObservableCollection<PackageItemViewModel> _packageItemViewModels;
        private PackageListViewModel _browsePackageListViewModel;
        private PackageListViewModel _installedPackageListViewModel;
        private PackageListViewModel _updatesPackageListViewModel;
        private PackageListViewModel _consolidatePackageListViewModel;

        /// <summary>
        /// Active <see cref="PackageListViewModel"/> in the foreground.
        /// </summary>
        public PackageListViewModel PackageListViewModel { get; private set; }

        public PackageManagerViewModel()
        {
            _packageItemViewModels = new ObservableCollection<PackageItemViewModel>();

            //TODO: SetActivePackageListViewModel(initialFilter);
        }

        public void SetActivePackageListViewModel(ItemFilter itemFilter)
        {
            switch (itemFilter)
            {
                case ItemFilter.All:
                    if (_browsePackageListViewModel is null)
                    {
                        _browsePackageListViewModel = new PackageListViewModel(_packageItemViewModels, fetchPageOnScroll: true);
                    }
                    PackageListViewModel = _browsePackageListViewModel;
                    break;
                case ItemFilter.Installed:
                    if (_installedPackageListViewModel is null)
                    {
                        _installedPackageListViewModel = new PackageListViewModel(_packageItemViewModels, fetchPageOnScroll: false);
                    }
                    PackageListViewModel = _installedPackageListViewModel;
                    break;
                case ItemFilter.UpdatesAvailable:
                    if (_updatesPackageListViewModel is null)
                    {
                        _updatesPackageListViewModel = new PackageListViewModel(_packageItemViewModels, fetchPageOnScroll: false);
                    }
                    PackageListViewModel = _updatesPackageListViewModel;
                    break;
                case ItemFilter.Consolidate:
                    if (_consolidatePackageListViewModel is null)
                    {
                        _consolidatePackageListViewModel = new PackageListViewModel(_packageItemViewModels, fetchPageOnScroll: false);
                    }
                    PackageListViewModel = _consolidatePackageListViewModel;
                    break;
            }
        }
    }
}
