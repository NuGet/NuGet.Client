// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using NuGet.Commands;
using NuGet.Frameworks;

namespace NuGet.VisualStudio
{
    public static class SolutionWorkspaceUtility
    {
        private static readonly string TargetPlatformIdentifier = "TargetPlatformIdentifier";
        private static readonly string TargetPlatformVersion = "TargetPlatformVersion";
        private static readonly string TargetPlatformMinVersion = "TargetPlatformMinVersion";
        private static readonly string TargetFrameworkMoniker = "TargetFrameworkMoniker";

        public static async Task<NuGetFramework> GetNuGetFrameworkAsync(
            IMSBuildProjectDataService dataService,
            string projectPath)
        {
            Assumes.Present(dataService);

            var targetPlatformIdentifier = await GetProjectPropertyAsync(dataService, TargetPlatformIdentifier);
            var targetPlatformVersion = await GetProjectPropertyAsync(dataService, TargetPlatformVersion);
            var targetPlatformMinVersion = await GetProjectPropertyAsync(dataService, TargetPlatformMinVersion);
            var targetFrameworkMoniker = await GetProjectPropertyAsync(dataService, TargetFrameworkMoniker);

            var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                projectFilePath: projectPath,
                targetFrameworks: null,
                targetFramework: null,
                targetFrameworkMoniker: targetFrameworkMoniker,
                targetPlatformIdentifier: targetPlatformIdentifier,
                targetPlatformVersion: targetPlatformVersion,
                targetPlatformMinVersion: targetPlatformMinVersion);

            var frameworkString = frameworkStrings.FirstOrDefault();

            if (!string.IsNullOrEmpty(frameworkString))
            {
                return NuGetFramework.Parse(frameworkString);
            }

            return NuGetFramework.UnsupportedFramework;
        }

        public static async Task<string> GetProjectPropertyAsync(IMSBuildProjectDataService dataService, string propertyName)
        {
            Assumes.Present(dataService);

            return (await dataService.GetProjectProperty(propertyName)).EvaluatedValue;
        }
    }
}
