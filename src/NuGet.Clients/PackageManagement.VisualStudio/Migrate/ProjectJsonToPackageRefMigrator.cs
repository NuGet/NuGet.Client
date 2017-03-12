// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class ProjectJsonToPackageRefMigrator
    {
        public static async Task MigrateAsync(LegacyCSProjPackageReferenceProject project, string dteProjectFullName)
        {
            var projectJsonFilePath = ProjectJsonPathUtilities.GetProjectConfigPath(Path.GetDirectoryName(project.MSBuildProjectPath),
                Path.GetFileNameWithoutExtension(project.MSBuildProjectPath));

            var packageSpec = JsonPackageSpecReader.GetPackageSpec(
                Path.GetFileNameWithoutExtension(project.MSBuildProjectPath),
                projectJsonFilePath);

            await MigrateDependencies(project, packageSpec);

            var buildProject = EnvDTEProjectUtility.AsMicrosoftBuildEvaluationProject(dteProjectFullName);

            MigrateRuntimes(packageSpec, buildProject);

            RemoveProjectJsonReference(buildProject, projectJsonFilePath);

            CreateBackup(project,
                projectJsonFilePath);
        }

        private static async Task MigrateDependencies(LegacyCSProjPackageReferenceProject project, PackageSpec packageSpec)
        {
            if (packageSpec.TargetFrameworks.Count > 1)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleFrameworks,
                        project.MSBuildProjectPath));
            }

            var dependencies = new List<LibraryDependency>();
            foreach (var targetFramework in packageSpec.TargetFrameworks)
            {
                dependencies.AddRange(targetFramework.Dependencies);
            }

            dependencies.AddRange(packageSpec.Dependencies);
            foreach (var dependency in dependencies)
            {
                var includeFlags = dependency.IncludeType;
                var privateAssetsFlag = dependency.SuppressParent;
                var metadataElements = new List<string>();
                var metadataValues = new List<string>();
                if (includeFlags != LibraryIncludeFlags.All)
                {
                    metadataElements.Add("IncludeAssets");
                    metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(includeFlags).Replace(',',';'));
                }

                if (privateAssetsFlag != LibraryIncludeFlagUtils.DefaultSuppressParent)
                {
                    metadataElements.Add("PrivateAssets");
                    metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(privateAssetsFlag).Replace(',',';'));
                }

                await project.InstallPackageWithMetadataAsync(dependency.Name, dependency.LibraryRange.VersionRange, metadataElements, metadataValues);
            }
        }

        private static void MigrateRuntimes(PackageSpec packageSpec, Microsoft.Build.Evaluation.Project buildProject)
        {
            var runtimes = packageSpec.RuntimeGraph.Runtimes;
            var supports = packageSpec.RuntimeGraph.Supports;
            var runtimeIdentifiers = new List<string>();
            var runtimeSupports = new List<string>();
            if (runtimes != null && runtimes.Count > 0)
            {
                runtimeIdentifiers.AddRange(runtimes.Keys);
                
            }

            if(supports != null && supports.Count > 0)
            {
                runtimeSupports.AddRange(supports.Keys);
            }

            var union = string.Join(";", runtimeIdentifiers.Union(runtimeSupports));
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                buildProject.SetProperty("RuntimeIdentifiers", union);
            });            
        }

        private static void RemoveProjectJsonReference(Microsoft.Build.Evaluation.Project buildProject, string projectJsonFilePath)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var projectJsonItem = buildProject.GetItems("None")
                .Where(t => string.Equals(t.EvaluatedInclude, Path.GetFileName(projectJsonFilePath), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

                if (projectJsonItem != null)
                {
                    buildProject.RemoveItem(projectJsonItem);
                }
            });
        }

        private static void CreateBackup(LegacyCSProjPackageReferenceProject project,
            string projectJsonFilePath)
        {
            var backupDirectory = Path.Combine(Path.GetDirectoryName(project.MSBuildProjectPath), "Backup");

            Directory.CreateDirectory(backupDirectory);

            var backupJsonFile = Path.Combine(backupDirectory, Path.GetFileName(projectJsonFilePath));
            FileUtility.Move(projectJsonFilePath, backupJsonFile);
            var backupProjectFile = Path.Combine(backupDirectory, Path.GetFileName(project.MSBuildProjectPath));
            File.Copy(project.MSBuildProjectPath, backupProjectFile);
        }        
    }
}
