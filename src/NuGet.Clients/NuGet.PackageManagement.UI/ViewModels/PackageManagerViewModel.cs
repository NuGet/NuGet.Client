// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;

namespace NuGet.PackageManagement.UI
{
    public class PackageManagerViewModel : ViewModelBase
    {
        private ObservableCollection<PackageItemViewModel> _packageItemViewModels;

        public PackageListViewModel PackageListViewModel { get; set; }

        public PackageManagerViewModel()
        {
            _packageItemViewModels = new ObservableCollection<PackageItemViewModel>();

            PackageListViewModel = new PackageListViewModel(_packageItemViewModels);
        }
    }
}
