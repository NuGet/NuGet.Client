// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIFactory
    {
        Task<INuGetUI> CreateAsync(params NuGetProject[] projects);
    }
}
