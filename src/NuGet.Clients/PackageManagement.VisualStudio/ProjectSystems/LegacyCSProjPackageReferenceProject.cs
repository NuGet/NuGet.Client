using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
/// <summary>
/// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
/// packages in a legacy CSProj with package references.
/// </summary>
    public class LegacyCSProjPackageReferenceProject : BuildIntegratedNuGetProject
    {
        private const string _assetsFileName = "project.assets.json";
        private const string _includeAssets = "IncludeAssets";
        private const string _excludeAssets = "ExcludeAssets";
        private const string _privateAssets = "PrivateAssets";

        private static Array _desiredPackageReferenceMetadata;

        private readonly IEnvDTEProjectAdapter _project;

        private IScriptExecutor _scriptExecutor;

        static LegacyCSProjPackageReferenceProject()
        {
            _desiredPackageReferenceMetadata = Array.CreateInstance(typeof(string), 3);
            _desiredPackageReferenceMetadata.SetValue(_includeAssets, 0);
            _desiredPackageReferenceMetadata.SetValue(_excludeAssets, 1);
            _desiredPackageReferenceMetadata.SetValue(_privateAssets, 2);
        }

        public LegacyCSProjPackageReferenceProject(
            IEnvDTEProjectAdapter project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            _project = project;

            AssetsFilePath = Path.Combine(_project.GetBaseIntermediatePath().Result ?? "", _assetsFileName);
            ProjectName = _project.Name;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _project.Name);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _project.UniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _project.ProjectFullPath);
        }

        #region IDependencyGraphProject

        public override string AssetsFilePath { get; }

        public override string ProjectName { get; }
        
        public override string MSBuildProjectPath => _project.ProjectFullPath;

        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if (_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }

                return _scriptExecutor;
            }
        }

        public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec;
            if (context.PackageSpecCache.TryGetValue(_project.ProjectFullPath, out packageSpec))
            {
                return new[] { packageSpec };
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            packageSpec = await GetPackageSpec();
            context.PackageSpecCache.Add(_project.ProjectFullPath, packageSpec);
            return new[] { packageSpec };
        }

        public override async Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            return
                await
                    ScriptExecutorUtil.ExecuteScriptAsync(identity, packageInstallPath, projectContext, ScriptExecutor,
                        _project.DTEProject, throwOnFailure);
        }

        #endregion

        #region NuGetProject

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return GetPackageReferences(await GetPackageSpec());
        }

        public override async Task<Boolean> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            try
            {
                // We don't adjust package reference metadata from UI
                await _project.AddOrUpdateLegacyCSProjPackageAsync(
                    packageIdentity.Id,
                    packageIdentity.Version.ToString(),
                    new string[] { },
                    new string[] { });
            }
            catch (Exception e)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, e.Message, packageIdentity, _project.Name);
                return false;
            }

            return true;
        }

        public override async Task<Boolean> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            try
            {
                await _project.RemoveLegacyCSProjPackageAsync(packageIdentity.Id);
            }
            catch (Exception e)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, e.Message, packageIdentity, _project.Name);
                return false;
            }

            return true;
        }

        #endregion

        private static string[] GetProjectReferences(PackageSpec packageSpec)
        {
            // There is only one target framework for legacy csproj projects
            var targetFramework = packageSpec.TargetFrameworks.FirstOrDefault();
            if (targetFramework == null)
            {
                return new string[] { };
            }

            return targetFramework.Dependencies
                .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.ExternalProject)
                .Select(d => d.LibraryRange.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static PackageReference[] GetPackageReferences(PackageSpec packageSpec)
        {
            var frameworkSorter = new NuGetFrameworkSorter();

            return packageSpec
                .TargetFrameworks
                .SelectMany(f => GetPackageReferences(f.Dependencies, f.FrameworkName))
                .GroupBy(p => p.PackageIdentity)
                .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                .ToArray();
        }

        private static IEnumerable<PackageReference> GetPackageReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework)
        {
            return libraries
                .Where(l => l.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(l => ToPackageReference(l, targetFramework));
        }

        private static PackageReference ToPackageReference(LibraryDependency library, NuGetFramework targetFramework)
        {
            var identity = new PackageIdentity(
                library.LibraryRange.Name,
                library.LibraryRange.VersionRange.MinVersion);

            return new PackageReference(identity, targetFramework);
        }

        private async Task<PackageSpec> GetPackageSpec()
        {
            var projectReferences = _project.GetLegacyCSProjProjectReferencesAsync(_desiredPackageReferenceMetadata)
                .Result
                .Select(ToProjectRestoreReference);

            var packageReferences = _project.GetLegacyCSProjPackageReferencesAsync(_desiredPackageReferenceMetadata)
                .Result
                .Select(ToPackageLibraryDependency);

            var projectTfi = new TargetFrameworkInformation()
            {
                FrameworkName = await _project.GetTargetNuGetFramework(),
                Dependencies = packageReferences.ToList()
            };

            // In legacy CSProj, we only have one target framework per project
            var tfis = new TargetFrameworkInformation[] { projectTfi };
            var packageSpec = new PackageSpec(tfis)
            {
                Name = _project.Name ?? _project.UniqueName,
                FilePath = _project.ProjectFullPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    OutputType = RestoreOutputType.NETCore,
                    OutputPath = await _project.GetBaseIntermediatePath(),
                    ProjectPath = _project.ProjectFullPath,
                    ProjectName = _project.Name ?? _project.UniqueName,
                    ProjectUniqueName = _project.ProjectFullPath,
                    OriginalTargetFrameworks = tfis
                        .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                        .ToList(),
                    TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>() {
                        new ProjectRestoreMetadataFrameworkInfo(tfis[0].FrameworkName)
                        {
                            ProjectReferences = projectReferences.ToList()
                        }
                    }
                }
            };

            return packageSpec;
        }

        private static ProjectRestoreReference ToProjectRestoreReference(LegacyCSProjProjectReference item)
        {
            var reference = new ProjectRestoreReference()
            {
                ProjectUniqueName = item.UniqueName,
                ProjectPath = item.UniqueName
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                reference,
                GetProjectMetadataValue(item, _includeAssets),
                GetProjectMetadataValue(item, _excludeAssets),
                GetProjectMetadataValue(item, _privateAssets));

            return reference;
        }

        private static LibraryDependency ToPackageLibraryDependency(LegacyCSProjPackageReference item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.Name,
                    versionRange: new VersionRange(new NuGetVersion(item.Version)),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                GetPackageMetadataValue(item, _includeAssets),
                GetPackageMetadataValue(item, _excludeAssets),
                GetPackageMetadataValue(item, _privateAssets));

            return dependency;
        }

        private static string GetProjectMetadataValue(LegacyCSProjProjectReference item, string metadataElement)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (string.IsNullOrEmpty(metadataElement))
            {
                throw new ArgumentNullException(nameof(metadataElement));
            }

            if (item.MetadataElements == null || item.MetadataValues == null)
            {
                return String.Empty; // no metadata for project
            }

            var index = Array.IndexOf(item.MetadataElements, metadataElement);
            if (index >= 0)
            {
                return item.MetadataValues.GetValue(index) as string;
            }

            return string.Empty;
        }

        private static string GetPackageMetadataValue(LegacyCSProjPackageReference item, string metadataElement)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (string.IsNullOrEmpty(metadataElement))
            {
                throw new ArgumentNullException(nameof(metadataElement));
            }

            if (item.MetadataElements == null || item.MetadataValues == null)
            {
                return String.Empty; // no metadata for package
            }

            var index = Array.IndexOf(item.MetadataElements, metadataElement);
            if (index >= 0)
            {
                return item.MetadataValues.GetValue(index) as string;
            }

            return string.Empty;
        }
    }
}
