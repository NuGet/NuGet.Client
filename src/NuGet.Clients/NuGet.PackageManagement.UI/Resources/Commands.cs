// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    public static class Commands
    {
        public static ICommand FocusOnSearchBox { get; } = new RoutedCommand();

        // The parameter of this command is PackageItemListViewModel
        public static ICommand UninstallPackageCommand { get; } = new RoutedCommand();

        // The parameter of this command is PackageItemListViewModel
        public static ICommand InstallPackageCommand { get; } = new RoutedCommand();

        // no parameters
        public static ICommand RestartSearchCommand { get; } = new RoutedCommand();

        // no parameters. Overridable by hosting app.
        public static ICommand ShowErrorsCommand { get; set; } = new RoutedCommand();
    }
}
