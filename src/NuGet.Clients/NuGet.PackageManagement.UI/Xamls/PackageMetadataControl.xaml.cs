// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataControl.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl
    {
        public ReadMePreviewViewModel ReadMePreviewViewModel { get; set; }

        public PackageMetadataControl()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (DataContext is DetailControlModel detailControlModel)
            {
                detailControlModel.PropertyChanged += PackageMetadataControl_DataContext_PropertyChanged;
                if (string.IsNullOrWhiteSpace(detailControlModel.PackagePath))
                {
                    tabReadMe.Visibility = Visibility.Collapsed;
                    tabPackageDetails.IsSelected = true;
                }
                else
                {
                    tabReadMe.Visibility = Visibility.Visible;
                }
                Visibility = Visibility.Visible;
            }
            else
            {
                tabReadMe.Visibility = Visibility.Collapsed;
                Visibility = Visibility.Collapsed;
            }
        }

        private void PackageMetadataControl_DataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is DetailControlModel detailControlModel)
            {
                if (e.PropertyName == nameof(detailControlModel.IsReadMeAvailable))
                {
                    if (!detailControlModel.IsReadMeAvailable)
                    {
                        tabReadMe.Visibility = Visibility.Collapsed;
                        tabPackageDetails.IsSelected = true;
                    }
                    else
                    {
                        tabReadMe.Visibility = Visibility.Visible;
                    }

                }

            }
        }
    }
}
