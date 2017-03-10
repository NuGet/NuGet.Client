// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ProjectJsonToPackageRefMigrator
    {
        private readonly LegacyCSProjPackageReferenceProject _project;
        private readonly Project _envdteProject;

        public ProjectJsonToPackageRefMigrator(LegacyCSProjPackageReferenceProject project, Project envDteProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _project = project;
            _envdteProject = envDteProject;            
        }

        internal string ProjectJsonFilePath { get; private set; }

        public async Task<ProjectJsonToPackageRefMigrateResult> MigrateAsync()
        {
            ProjectJsonFilePath = ProjectJsonPathUtilities.GetProjectConfigPath(Path.GetDirectoryName(_project.MSBuildProjectPath),
                Path.GetFileNameWithoutExtension(_project.MSBuildProjectPath));

            var packageSpec = JsonPackageSpecReader.GetPackageSpec(
                Path.GetFileNameWithoutExtension(_project.MSBuildProjectPath),
                ProjectJsonFilePath);

            await MigrateDependencies(packageSpec);

            var buildProject = EnvDTEProjectUtility.AsMicrosoftBuildEvaluationProject(_envdteProject.FullName);

            MigrateRuntimes(packageSpec, buildProject);

            RemoveProjectJsonReference(buildProject);

            string backupProjectFile, backupJsonFile;
            CreateBackupAndSave(out backupProjectFile, out backupJsonFile);

            return new ProjectJsonToPackageRefMigrateResult(true, backupProjectFile, backupJsonFile);
        }

        private async Task MigrateDependencies(PackageSpec packageSpec)
        {
            if (packageSpec.TargetFrameworks.Count > 1)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleFrameworksUwp,
                        _project.MSBuildProjectPath));
            }

            var dependencies = new List<LibraryDependency>();
            foreach (var targetFramework in packageSpec.TargetFrameworks)
            {
                if (targetFramework.FrameworkName != NuGetFramework.Parse("uap10.0"))
                {
                    throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_TargetFrameworkNotUwp,
                        _project.MSBuildProjectPath));
                }
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

                await _project.InstallPackageWithMetadataAsync(dependency.Name, dependency.LibraryRange.VersionRange, metadataElements, metadataValues);
            }
        }

        private void MigrateRuntimes(PackageSpec packageSpec, Microsoft.Build.Evaluation.Project buildProject)
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
            buildProject.SetProperty("RuntimeIdentifiers", union);
        }

        private void RemoveProjectJsonReference(Microsoft.Build.Evaluation.Project buildProject)
        {
            var projectJsonItem = buildProject.GetItems("None")
                .Where(t => string.Equals(t.EvaluatedInclude, Path.GetFileName(ProjectJsonFilePath), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (projectJsonItem != null)
            {
                buildProject.RemoveItem(projectJsonItem);
            }

        }

        private void CreateBackupAndSave(out string backupProjectFile, out string backupJsonFile)
        {
            var backupDirectory = Path.Combine(Path.GetDirectoryName(_project.MSBuildProjectPath), "Backup");
            if(!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            backupJsonFile = Path.Combine(backupDirectory, Path.GetFileName(ProjectJsonFilePath));
            File.Move(ProjectJsonFilePath, backupJsonFile);
            backupProjectFile = Path.Combine(backupDirectory, Path.GetFileName(_project.MSBuildProjectPath));
            File.Copy(_project.MSBuildProjectPath, backupProjectFile);
            _envdteProject.Save();
        }
        
    }
}
