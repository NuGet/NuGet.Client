// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageSourceMappingActionControl.xaml
    /// </summary>
    public partial class PackageSourceMappingActionControl : UserControl
    {
        private PackageSourceMappingActionViewModel _packageSourceMappingActionViewModel;

        public PackageSourceMappingActionControl()
        {
            InitializeComponent();
        }

        internal PackageSourceMappingActionViewModel ViewModel
        {
            get
            {
                if (_packageSourceMappingActionViewModel == null) _packageSourceMappingActionViewModel = (PackageSourceMappingActionViewModel)DataContext;
                return _packageSourceMappingActionViewModel;
            }
            set
            {
                _packageSourceMappingActionViewModel = value;
            }
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            ViewModel.UIController.LaunchNuGetOptionsDialog(OptionsPage.PackageSourceMapping);
        }
    }
}
