// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.Test
{
    public class TestBuildIntegratedNuGetProject : ProjectJsonBuildIntegratedNuGetProject
    {
        public IReadOnlyList<ExternalProjectReference> ProjectClosure { get; set; }

        public TestBuildIntegratedNuGetProject(
            string jsonConfig,
            IMSBuildNuGetProjectSystem msbuildProjectSystem) : base(
                jsonConfig,
                Path.Combine(
                    msbuildProjectSystem.ProjectFullPath,
                    $"{msbuildProjectSystem.ProjectName}.csproj"),
                msbuildProjectSystem)
        {
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, msbuildProjectSystem.ProjectName);
            ProjectClosure = new List<ExternalProjectReference>();
        }
    }
}
