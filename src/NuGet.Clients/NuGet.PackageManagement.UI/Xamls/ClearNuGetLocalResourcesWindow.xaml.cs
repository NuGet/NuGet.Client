// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.PlatformUI;
using NuGet.PackageManagement.UI.ViewModels;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for ClearNuGetLocalResourcesWindow.xaml
    /// </summary>
    public partial class ClearNuGetLocalResourcesWindow : DialogWindow
    {
        public ClearNuGetLocalResourcesWindow(ClearNuGetLocalsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void DialogWindow_ContentRendered(object sender, System.EventArgs e)
        {
            (DataContext as ClearNuGetLocalsViewModel)?.Execute();
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
