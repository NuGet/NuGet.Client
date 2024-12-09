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
        public ClearNuGetLocalResourcesWindow()
        {
            DataContext = new ClearNuGetLocalsViewModel();
            InitializeComponent();
        }
    }
}
