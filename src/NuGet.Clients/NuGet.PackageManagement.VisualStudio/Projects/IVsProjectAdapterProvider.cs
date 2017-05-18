// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IVsProjectAdapterProvider
    {
        IVsProjectAdapter CreateVsProject(EnvDTE.Project dteProject);
        Task<IVsProjectAdapter> CreateVsProjectAsync(IVsHierarchy project);
    }
}