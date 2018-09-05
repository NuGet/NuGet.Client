// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Project system capabilities for deferred project.
    /// </summary>
    internal class DeferredProjectCapabilities : IProjectSystemCapabilities
    {
        public bool SupportsPackageReferences { get; set; }
    }
}
