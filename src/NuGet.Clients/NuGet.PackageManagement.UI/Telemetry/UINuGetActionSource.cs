// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Tracks where a NuGet action (install, update or uninstall) over one or more packages were triggered in PM UI
    /// </summary>
    public enum UINuGetActionSource
    {
        /// <summary>
        /// Packages list (InfiniteScrollList), left side of PM UI
        /// </summary>
        PackagesList,

        /// <summary>
        /// Package details pane (DetailControl), right side of PM UI
        /// </summary>
        DetailsPane,

        /// <summary>
        /// Action comes from 'Update' button in Updates tab
        /// </summary>
        UpdateButton,
    }
}
