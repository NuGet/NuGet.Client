// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InstallPreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : VsDialogWindow
    {
        public PreviewWindow()
        {
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
