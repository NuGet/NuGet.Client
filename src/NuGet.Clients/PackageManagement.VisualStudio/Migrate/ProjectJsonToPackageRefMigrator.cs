using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using Task = System.Threading.Tasks.Task;
using EnvDTE;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ProjectJsonToPackageRefMigrator
    {
        private LegacyCSProjPackageReferenceProject _project;
        private IVsBuildPropertyStorage _buildPropertyStorage;
        private Project _envdteProject;
        private IVsHierarchy _ivsHierarchy;
        public ProjectJsonToPackageRefMigrator(LegacyCSProjPackageReferenceProject project, Project envDteProject, IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _project = project;
            _envdteProject = envDteProject;
            _ivsHierarchy = hierarchy;
            _buildPropertyStorage = hierarchy as IVsBuildPropertyStorage;
            if (_buildPropertyStorage == null)
            {
                throw new InvalidOperationException(string.Format(
                    Strings.ProjectCouldNotBeCastedToBuildPropertyStorage,
                    project.MSBuildProjectPath));
            }

            ProjectJsonFilePath = ProjectJsonPathUtilities.GetProjectConfigPath(Path.GetDirectoryName(project.MSBuildProjectPath),
                Path.GetFileNameWithoutExtension(project.MSBuildProjectPath));
        }

        internal string ProjectJsonFilePath { get; private set; }

        public async Task<ProjectJsonToPackageRefMigrateResult> MigrateAsync()
        {
            var packageSpec = await Task.Run(() =>
                                    JsonPackageSpecReader.GetPackageSpec(Path.GetFileNameWithoutExtension(_project.MSBuildProjectPath),
                                                                         ProjectJsonFilePath));
            await MigrateDependencies(packageSpec);
            var buildProject = EnvDTEProjectUtility.AsMicrosoftBuildEvaluationProject(_envdteProject.FullName);
            MigrateRuntimes(packageSpec, buildProject);
            RemoveProjectJsonReference(buildProject);
            string backupProjectFile, backupJsonFile;
            CreateBackupAndSave(out backupProjectFile, out backupJsonFile);
            return await Task.FromResult(new ProjectJsonToPackageRefMigrateResult(true, backupProjectFile, backupJsonFile));
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
                    metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(includeFlags));
                }

                if (privateAssetsFlag != LibraryIncludeFlagUtils.DefaultSuppressParent)
                {
                    metadataElements.Add("PrivateAssets");
                    metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(privateAssetsFlag));
                }
                await _project.InstallPackageWithMetadataAsync(dependency.Name, dependency.LibraryRange.VersionRange, metadataElements, metadataValues);
            }
        }

        private void MigrateRuntimes(PackageSpec packageSpec, Microsoft.Build.Evaluation.Project buildProject)
        {
            var runtimes = packageSpec.RuntimeGraph.Runtimes;
            if (runtimes != null && runtimes.Count > 0)
            {
                var runtimeIdentifierValues = string.Join(";", runtimes.Keys);
                buildProject.SetProperty("RuntimeIdentifiers", runtimeIdentifierValues);
            }
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
