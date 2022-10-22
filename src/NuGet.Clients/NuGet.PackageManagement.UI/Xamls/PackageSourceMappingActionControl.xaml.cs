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
        private PackageDetailControlModel _packageDetailControlModel;

        public PackageSourceMappingActionControl()
        {
            InitializeComponent();
        }

        internal PackageDetailControlModel PackageDetailControlModel
        {
            get
            {
                if (_packageDetailControlModel == null) _packageDetailControlModel = (PackageDetailControlModel)DataContext;
                return _packageDetailControlModel;
            }
            set
            {
                _packageDetailControlModel = value;
            }
        }

        private void NewMapping_Checked(object sender, RoutedEventArgs e)
        {
            PackageDetailControlModel?.UpdateIsInstallorUpdateButtonEnabled();
        }

        private void SettingsButtonClicked(object sender, EventArgs e)
        {
            PackageDetailControlModel.UIController.LaunchNuGetOptionsDialog(OptionsPage.PackageSourceMapping);
        }
    }
}
