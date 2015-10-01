// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// This control is used as list items in the package list. Its DataContext is
    /// SearchResultPackageMetadata.
    /// </summary>
    public partial class PackageItemControl : UserControl
    {
        public PackageItemControl()
        {
            InitializeComponent();
            this.DataContextChanged += PackageItemControl_DataContextChanged;
        }

        private void PackageItemControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var package = DataContext as SearchResultPackageMetadata;
            if (package != null)
            {
                package.PropertyChanged += (s, arg) =>
                {
                    if (arg.PropertyName == nameof(package.Status))
                    {
                        UpdateButtonVisibility(package);
                    }
                };
            }

            UpdateButtonVisibility(package);
        }

        private void Package_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            throw new System.NotImplementedException();
        }

        // Set the visiblity values of the install, uninstall and update buttons
        private void UpdateButtonVisibility(SearchResultPackageMetadata package)
        {
            if (package == null)
            {
                return;
            }

            if (package.IsSolution)
            {
                _installButton.Visibility = Visibility.Collapsed;
                _updateButton.Visibility = Visibility.Collapsed;
                _uninstallButton.Visibility = Visibility.Collapsed;

                return;
            }

            if (this.IsMouseOver)
            {
                if (package.Status == PackageStatus.Installed)
                {
                    _uninstallButton.Visibility = Visibility.Visible;

                    _installButton.Visibility = Visibility.Hidden;
                    _updateButton.Visibility = Visibility.Hidden;
                }
                else if (package.Status == PackageStatus.NotInstalled)
                {
                    _installButton.Visibility = Visibility.Visible;

                    _uninstallButton.Visibility = Visibility.Hidden;
                    _updateButton.Visibility = Visibility.Hidden;
                }
                else if (package.Status == PackageStatus.UpdateAvailable)
                {
                    _uninstallButton.Visibility = Visibility.Visible;
                    _updateButton.Visibility = Visibility.Visible;

                    _installButton.Visibility = Visibility.Hidden;
                }                
            }
            else
            {
                _installButton.Visibility = Visibility.Hidden;
                _updateButton.Visibility = Visibility.Hidden;
                _uninstallButton.Visibility = Visibility.Hidden;
            }
        }

        private void UninstallButtonClicked(object sender, RoutedEventArgs e)
        {
            Commands.UninstallPackageCommand.Execute(this.DataContext, this);
        }

        private void InstallButtonClicked(object sender, RoutedEventArgs e)
        {
            Commands.InstallPackageCommand.Execute(this.DataContext, this);
        }

        private void UserControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var package = DataContext as SearchResultPackageMetadata;
            UpdateButtonVisibility(package);
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var package = DataContext as SearchResultPackageMetadata;
            UpdateButtonVisibility(package);
        }
    }
}
