// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// This control is used as list items in the package list. Its DataContext is
    /// PackageItemListViewModel.
    /// </summary>
    public partial class PackageItemControl : UserControl
    {
        public PackageItemControl()
        {
            InitializeComponent();
        }

        private void Package_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void UninstallButtonClicked(object sender, RoutedEventArgs e)
        {
            Commands.UninstallPackageCommand.Execute(this.DataContext, this);
        }

        private void InstallButtonClicked(object sender, RoutedEventArgs e)
        {
            Commands.InstallPackageCommand.Execute(this.DataContext, this);
        }
    }
}
