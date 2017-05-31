// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;

namespace NuGet.VisualStudio
{
    public interface IDeferredProjectWorkspaceService
    {
        Task<bool> EntityExists(string filePath);

        Task<IEnumerable<string>> GetProjectReferencesAsync(string projectFilePath);

        Task<IMSBuildProjectDataService> GetMSBuildProjectDataServiceAsync(string projectFilePath, string targetFramework = null);
    }
}