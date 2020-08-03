// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIFactory
    {
        INuGetUI Create(params ProjectContextInfo[] projects);
    }
}
