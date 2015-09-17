// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.UI
{
    [Export(typeof(INuGetUIFactory))]
    public class NuGetUIFactory : INuGetUIFactory
    {
        /// <summary>
        /// Returns the UI for the project or given set of projects.
        /// </summary>
        public INuGetUI Create(
            INuGetUIContext uiContext,
            NuGetUIProjectContext uiProjectContext)
        {
            return new NuGetUI(uiContext, uiProjectContext);
        }
    }
}
