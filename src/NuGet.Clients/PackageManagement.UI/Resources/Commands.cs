// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    public static class Commands
    {
        public static readonly RoutedCommand FocusOnSearchBox = new RoutedCommand();

        // The parameter of this command is PackageItemListViewModel
        public static readonly RoutedCommand UninstallPackageCommand = new RoutedCommand();

        // The parameter of this command is PackageItemListViewModel
        public static readonly RoutedCommand InstallPackageCommand = new RoutedCommand();
    }
}
