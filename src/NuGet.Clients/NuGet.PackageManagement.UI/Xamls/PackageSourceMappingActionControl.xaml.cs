// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageSourceMappingActionControl.xaml
    /// </summary>
    public partial class PackageSourceMappingActionControl : UserControl
    {
        private DetailControlModel _detailControlModel;

        public PackageSourceMappingActionControl()
        {
            InitializeComponent();
        }

        internal DetailControlModel DetailControlModel
        {
            get
            {
                if (_detailControlModel == null) _detailControlModel = (DetailControlModel)DataContext;
                return _detailControlModel;
            }
            set
            {
                _detailControlModel = value;
            }
        }

        private void NewMapping_Checked(object sender, RoutedEventArgs e)
        {
            //DetailControlModel?.UpdateIsInstallorUpdateButtonEnabled();
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            DetailControlModel.UIController.LaunchNuGetOptionsDialog(OptionsPage.PackageSourceMapping);
        }
    }
}
