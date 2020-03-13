// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// This control is used as list items in the package list. Its DataContext is
    /// PackageItemListViewModel.
    /// </summary>
    public partial class PackageItemControl : UserControl
    {
        public PackageItemControl()
        {
            InitializeComponent();
        }
    }
}
