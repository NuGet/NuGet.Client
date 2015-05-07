// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for ProjectList.xaml
    /// </summary>
    public partial class ProjectList : UserControl
    {
        public ProjectList()
        {
            InitializeComponent();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            model.CheckAllProjects();
        }

        private void _checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            model.UncheckAllProjects();
        }
    }
}
