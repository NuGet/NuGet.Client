// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.ViewModels;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataControl.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl, IDisposable
    {
        private bool _initialVisibilitySet = false;
        private ReadMePreviewViewModel _readMePreviewViewModel;

        public PackageMetadataControl()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
            ReadMePreviewViewModel = new ReadMePreviewViewModel();
            _packageMetadataReadMeControl.DataContext = ReadMePreviewViewModel;
            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }

        public ReadMePreviewViewModel ReadMePreviewViewModel { get => _readMePreviewViewModel; set => _readMePreviewViewModel = value; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _packageMetadataReadMeControl?.Dispose();
            }
        }

        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailControlModel detailControlModel)
            {
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void TabReadMe_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!_initialVisibilitySet && tabReadMe.IsVisible)
            {
                tabReadMe.IsSelected = true;
            }
            if (!tabReadMe.IsVisible)
            {
                tabPackageDetails.IsSelected = true;
            }
            _initialVisibilitySet = true;
        }
    }
}
