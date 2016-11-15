// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    public interface INuGetUIContextFactory
    {
        INuGetUIContext Create(NuGetPackage package, IEnumerable<NuGetProject> projects);
    }
}
