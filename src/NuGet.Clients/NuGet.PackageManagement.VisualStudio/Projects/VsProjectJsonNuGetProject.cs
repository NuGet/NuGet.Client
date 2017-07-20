// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    internal class VsProjectJsonNuGetProject : ProjectJsonNuGetProject
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;

        public VsProjectJsonNuGetProject(
            string jsonConfigPath,
            string msbuildProjectFilePath,
            IVsProjectAdapter vsProjectAdapter,
            INuGetProjectServices projectServices)
            : base(jsonConfigPath, msbuildProjectFilePath)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(projectServices);

            _vsProjectAdapter = vsProjectAdapter;

            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, _vsProjectAdapter.ProjectId);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _vsProjectAdapter.CustomUniqueName);

            ProjectServices = projectServices;
        }

        protected override async Task<string> GetBaseIntermediatePathAsync()
        {
            var baseIntermediatePath = await ProjectServices.BuildProperties.GetPropertyValueAsync(ProjectBuildProperties.BaseIntermediateOutputPath);

            if (string.IsNullOrEmpty(baseIntermediatePath))
            {
                throw new InvalidDataException(string.Format(
                    Strings.BaseIntermediateOutputPathNotFound,
                    MSBuildProjectPath));
            }

            return UriUtility.GetAbsolutePathFromFile(MSBuildProjectPath, baseIntermediatePath);
        }

        public override async Task<string> GetCacheFilePathAsync()
        {
            return NoOpRestoreUtilities.GetProjectCacheFilePath(await GetBaseIntermediatePathAsync(), MSBuildProjectPath);
        }

        protected override async Task UpdateInternalTargetFrameworkAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Override the JSON TFM value from the csproj for UAP framework
            if (InternalMetadata.TryGetValue(NuGetProjectMetadataKeys.TargetFramework, out var targetFramework))
            {
                var jsonTargetFramework = targetFramework as NuGetFramework;
                if (IsUAPFramework(jsonTargetFramework))
                {
                    var platformMinVersionString = await _vsProjectAdapter
                        .BuildProperties
                        .GetPropertyValueAsync(ProjectBuildProperties.TargetPlatformMinVersion);

                    var platformMinVersion = !string.IsNullOrEmpty(platformMinVersionString)
                        ? new Version(platformMinVersionString)
                        : null;

                    if (platformMinVersion != null && jsonTargetFramework.Version != platformMinVersion)
                    {
                        // Found the TPMinV in csproj and it is different from project json's framework version,
                        // store this as a new target framework to be replaced in project.json
                        var newTargetFramework = new NuGetFramework(jsonTargetFramework.Framework, platformMinVersion);
                        InternalMetadata[NuGetProjectMetadataKeys.TargetFramework] = newTargetFramework;
                    }
                }
            }
        }
    }
}
