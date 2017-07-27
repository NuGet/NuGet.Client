// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface ILightWeightProjectWorkspaceService
    {
        Task<bool> EntityExists(string filePath);

        Task<IEnumerable<string>> GetProjectReferencesAsync(string projectFilePath);

        Task<IMSBuildProjectDataService> GetMSBuildProjectDataService(string projectFilePath, string targetFramework = "");

        Task<IEnumerable<MSBuildProjectItemData>> GetProjectItemsAsync(IMSBuildProjectDataService dataService, string itemType);

        Task<string> GetProjectPropertyAsync(IMSBuildProjectDataService dataService, string propertyName);
    }
}