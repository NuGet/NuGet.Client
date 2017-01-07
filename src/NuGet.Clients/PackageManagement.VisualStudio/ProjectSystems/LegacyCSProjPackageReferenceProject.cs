// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
    /// packages in a legacy CSProj with package references.
    /// </summary>
    public class LegacyCSProjPackageReferenceProject : BuildIntegratedNuGetProject
    {
        private const string _includeAssets = "IncludeAssets";
        private const string _excludeAssets = "ExcludeAssets";
        private const string _privateAssets = "PrivateAssets";

        private static Array _desiredPackageReferenceMetadata;

        private readonly IEnvDTEProjectAdapter _project;

        private IScriptExecutor _scriptExecutor;
        private string _projectName;
        private string _projectUniqueName;
        private string _projectFullPath;
        private bool _callerIsUnitTest;

        static LegacyCSProjPackageReferenceProject()
        {
            _desiredPackageReferenceMetadata = Array.CreateInstance(typeof(string), 3);
            _desiredPackageReferenceMetadata.SetValue(_includeAssets, 0);
            _desiredPackageReferenceMetadata.SetValue(_excludeAssets, 1);
            _desiredPackageReferenceMetadata.SetValue(_privateAssets, 2);
        }

        public LegacyCSProjPackageReferenceProject(
            IEnvDTEProjectAdapter project,
            string projectId,
            bool callerIsUnitTest = false)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            _project = project;
            _projectName = _project.Name;
            _projectUniqueName = _project.UniqueName;
            _projectFullPath = _project.ProjectFullPath;
            _callerIsUnitTest = callerIsUnitTest;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);
        }

        public override string ProjectName => _projectName;

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

        public override async Task<string> GetAssetsFilePathAsync()
        {
            return Path.Combine(await GetBaseIntermediatePathAsync(), LockFileFormat.AssetsFileName);
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

        #region IDependencyGraphProject

        public override string MSBuildProjectPath => _projectFullPath;

        public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec;
            if (context == null || !context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out packageSpec))
            {
                packageSpec = await GetPackageSpecAsync();
                if (packageSpec == null)
                {
                    throw new InvalidOperationException(
                        string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
                }
                context?.PackageSpecCache.Add(_projectFullPath, packageSpec);
            }

            return new[] { packageSpec };
        }

        #endregion

        #region NuGetProject

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return GetPackageReferences(await GetPackageSpecAsync());
        }

        public override async Task<Boolean> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext nuGetProjectContext,
            BuildIntegratedInstallationContext installationContext,
            CancellationToken token)
        {
            var success = false;

            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // We don't adjust package reference metadata from UI
                _project.AddOrUpdateLegacyCSProjPackage(
                    packageId,
                    range.MinVersion.ToNormalizedString(),
                    metadataElements: new string[0],
                    metadataValues: new string[0]);

                success = true;
            });

            return success;
        }

        public override async Task<Boolean> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var success = false;
            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _project.RemoveLegacyCSProjPackage(packageIdentity.Id);

                success = true;
            });

            return success;
        }

        #endregion

        private async Task<string> GetBaseIntermediatePathAsync()
        {
            return await RunOnUIThread(GetBaseIntermediatePath);
        }

        private string GetBaseIntermediatePath()
        {
            EnsureUIThread();

            var baseIntermediatePath = _project.BaseIntermediateOutputPath;

            if (string.IsNullOrEmpty(baseIntermediatePath) || !Directory.Exists(baseIntermediatePath))
            {
                throw new InvalidDataException(nameof(_project.BaseIntermediateOutputPath));
            }

            return baseIntermediatePath;
        }

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

        private async Task<PackageSpec> GetPackageSpecAsync()
        {
            return await RunOnUIThread(GetPackageSpec);
        }

        /// <summary>
        /// Emulates a JSON deserialization from project.json to PackageSpec in a post-project.json world
        /// </summary>
        private PackageSpec GetPackageSpec()
        {
            EnsureUIThread();

            var projectReferences = _project.GetLegacyCSProjProjectReferences(_desiredPackageReferenceMetadata)
                .Select(ToProjectRestoreReference);

            var packageReferences = _project.GetLegacyCSProjPackageReferences(_desiredPackageReferenceMetadata)
                .Select(ToPackageLibraryDependency).ToList();

            var packageTargetFallback = _project.PackageTargetFallback?.Split(new[] { ';' })
                .Select(NuGetFramework.Parse)
                .ToList();

            var projectTfi = new TargetFrameworkInformation()
            {
                FrameworkName = _project.TargetNuGetFramework,
                Dependencies = packageReferences,
                Imports = packageTargetFallback ?? new List<NuGetFramework>()
            };

            if ((projectTfi.Imports?.Count ?? 0) > 0)
            {
                projectTfi.FrameworkName = new FallbackFramework(projectTfi.FrameworkName, packageTargetFallback);
            }

            // Build up runtime information.
            var runtimes = _project.Runtimes;
            var supports = _project.Supports;
            var runtimeGraph = new RuntimeGraph(runtimes, supports);

            // In legacy CSProj, we only have one target framework per project
            var tfis = new TargetFrameworkInformation[] { projectTfi };

            return new PackageSpec(tfis)
            {
                Name = _projectName ?? _projectUniqueName,
                Version = new NuGetVersion(_project.Version),
                Authors = new string[] { },
                Owners = new string[] { },
                Tags = new string[] { },
                ContentFiles = new string[] { },
                Dependencies = packageReferences,
                FilePath = _projectFullPath,
                RuntimeGraph = runtimeGraph,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    OutputPath = GetBaseIntermediatePath(),
                    ProjectPath = _projectFullPath,
                    ProjectName = _projectName ?? _projectUniqueName,
                    ProjectUniqueName = _projectFullPath,
                    OriginalTargetFrameworks = tfis
                        .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                        .ToList(),
                    TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>()
                    {
                        new ProjectRestoreMetadataFrameworkInfo(tfis[0].FrameworkName)
                        {
                            ProjectReferences = projectReferences?.ToList()
                        }
                    }
                }
            };
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
                    versionRange: new VersionRange(new NuGetVersion(item.Version), originalString: item.Version),
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

        private async Task<T> RunOnUIThread<T>(Func<T> uiThreadFunction)
        {
            if (_callerIsUnitTest)
            {
                return uiThreadFunction();
            }

            T result = default(T);
            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                result = uiThreadFunction();
            });

            return result;
        }

        private void EnsureUIThread()
        {
            if (!_callerIsUnitTest)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
            }
        }
    }
}
