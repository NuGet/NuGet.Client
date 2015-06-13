// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;

namespace NuGetConsole
{
    /// <summary>
    /// Interaction logic for ConsoleContainer.xaml
    /// </summary>
    public partial class ConsoleContainer : UserControl
    {
        public ConsoleContainer(
            ISolutionManager solutionManager,
            IProductUpdateService productUpdateService,
            IPackageRestoreManager packageRestoreManager,
            IDeleteOnRestartManager deleteOnRestartManager,
            IVsShell4 shell)
        {

            InitializeComponent();

            RootLayout.Children.Add(new ProductUpdateBar(productUpdateService));
            RootLayout.Children.Add(new PackageRestoreBar(solutionManager, packageRestoreManager));
            RootLayout.Children.Add(new RestartRequestBar(deleteOnRestartManager, shell));

            // Set DynamicResource binding in code
            // The reason we can't set it in XAML is that the VsBrushes class come from either
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly,
            // depending on whether NuGet runs inside VS10 or VS11.
            InitializeText.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.WindowTextKey);
        }

        public void AddConsoleEditor(UIElement content)
        {
            Grid.SetRow(content, 1);
            RootLayout.Children.Add(content);
        }

        public void NotifyInitializationCompleted()
        {
            RootLayout.Children.Remove(InitializeText);
        }
    }
}
