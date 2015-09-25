// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
#if DNX451
using Microsoft.Build.Execution;
#endif

namespace NuGet.CommandLine.XPlat
{
    public class MsBuildUtility
    {
        private const string GetProjectReferenceBuildTarget = "GetProjectsReferencingProjectJson";
        private const string ProjectReferencesKey = "ProjectReferences";
        private static readonly HashSet<string> _msbuildExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj",
            ".vbproj",
            ".fsproj",
        };

        public static bool IsMsBuildBasedProject(string projectFullPath)
        {
            return _msbuildExtensions.Contains(Path.GetExtension(projectFullPath));
        }

#if !DNXCORE50
        public static IEnumerable<string> GetProjectReferences(string projectFullPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectFullPath);
            var project = new ProjectInstance(projectFullPath);

            var manager = new BuildManager();
            var result = manager.Build(
                new BuildParameters(),
                new BuildRequestData(project, new[] { GetProjectReferenceBuildTarget }));

            TargetResult targetResult;
            if (result.ResultsByTarget.TryGetValue(GetProjectReferenceBuildTarget, out targetResult))
            {
                if (targetResult.ResultCode == TargetResultCode.Success &&
                    targetResult.Items.Length > 0)
                {
                    var taskItem = targetResult.Items[0];
                    var metadataValue = taskItem.GetMetadata(ProjectReferencesKey);

                    var projectRelativePaths = metadataValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var relativePath in projectRelativePaths)
                    {
                        var resolvedPath = Path.GetFullPath(Path.Combine(projectDirectory, relativePath));
                        yield return resolvedPath;
                    }
                }
                else if (targetResult.Exception != null)
                {
                    throw targetResult.Exception;
                }
            }
            else if (result.Exception != null)
            {
                throw result.Exception;
            }
        }
#endif
    }
}
