// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
    /// packages in a legacy CSProj with package references.
    /// </summary>
    public sealed class LegacyPackageReferenceProject : BuildIntegratedNuGetProject
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;

        private string _projectName;
        private string _projectUniqueName;
        private string _projectFullPath;
        private Dictionary<string, ProjectInstalledPackage> _installedPackages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastTimeAssetsModified;

        public LegacyPackageReferenceProject(
            IVsProjectAdapter vsProjectAdapter,
            string projectId,
            INuGetProjectServices projectServices,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.NotNullOrEmpty(projectId);
            Assumes.Present(projectServices);
            Assumes.Present(threadingService);

            _vsProjectAdapter = vsProjectAdapter;
            _threadingService = threadingService;

            _projectName = _vsProjectAdapter.ProjectName;
            _projectUniqueName = _vsProjectAdapter.UniqueName;
            _projectFullPath = _vsProjectAdapter.FullProjectPath;

            ProjectStyle = ProjectStyle.PackageReference;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);

            ProjectServices = projectServices;
        }

        #region BuildIntegratedNuGetProject

        public override string ProjectName => _projectName;

        public override async Task<string> GetAssetsFilePathAsync()
        {
            return await GetAssetsFilePathAsync(shouldThrow: true);
        }

        public override async Task<string> GetCacheFilePathAsync()
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
            return NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: GetMSBuildProjectExtensionsPath());
        }

        public override async Task<string> GetAssetsFilePathOrNullAsync()
        {
            return await GetAssetsFilePathAsync(shouldThrow: false);
        }

        private async Task<string> GetAssetsFilePathAsync(bool shouldThrow)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var msbuildProjectExtensionsPath = GetMSBuildProjectExtensionsPath(shouldThrow);

            if (msbuildProjectExtensionsPath == null)
            {
                return null;
            }

            return Path.Combine(msbuildProjectExtensionsPath, LockFileFormat.AssetsFileName);
        }

        #endregion BuildIntegratedNuGetProject

        #region IDependencyGraphProject

        public override string MSBuildProjectPath => _projectFullPath;

        public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            var (dgSpec, _) = await GetPackageSpecsAndAdditionalMessagesAsync(context);
            return dgSpec;
        }

        public override async Task<(IReadOnlyList<PackageSpec> dgSpecs, IReadOnlyList<IAssetsLogMessage> additionalMessages)> GetPackageSpecsAndAdditionalMessagesAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec;
            if (context == null || !context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out packageSpec))
            {
                packageSpec = await GetPackageSpecAsync(context.Settings);
                if (packageSpec == null)
                {
                    throw new InvalidOperationException(
                        string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
                }
                context?.PackageSpecCache.Add(_projectFullPath, packageSpec);
            }

            return (new[] { packageSpec }, null);
        }

        private async Task<bool> IsCentralPackageManagementVersionsEnabledAsync()
        {
            return MSBuildStringUtility.IsTrue(await _vsProjectAdapter.GetPropertyValueAsync(ProjectBuildProperties.ManagePackageVersionsCentrally));
        }

        private async Task<Dictionary<string, CentralPackageVersion>> GetCentralPackageVersionsAsync()
        {
            IEnumerable<(string PackageId, string Version)> packageVersions =
                        (await _vsProjectAdapter.GetBuildItemInformationAsync(ProjectBuildProperties.PackageVersion, ProjectBuildProperties.Version))
                        .Select(item => (PackageId: item.ItemId, Version: item.ItemMetadata.FirstOrDefault()));

            return packageVersions
                .Select(item => ToCentralPackageVersion(item.PackageId, item.Version))
                .Distinct(CentralPackageVersionNameComparer.Default)
                .ToDictionary(cpv => cpv.Name);
        }


        private CentralPackageVersion ToCentralPackageVersion(string packageId, string version)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (string.IsNullOrEmpty(version))
            {
                return new CentralPackageVersion(packageId, VersionRange.All);
            }

            return new CentralPackageVersion(packageId, VersionRange.Parse(version));
        }

        #endregion

        #region NuGetProject

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            // Settings are not needed for this purpose, this only finds the installed packages
            var packageSpec = await GetPackageSpecAsync(NullSettings.Instance);
            return await GetPackageReferencesAsync(packageSpec);
        }

        public override async Task<bool> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext _,
            BuildIntegratedInstallationContext __,
            CancellationToken token)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: packageId,
                    versionRange: range,
                    typeConstraint: LibraryDependencyTarget.Package),
                SuppressParent = __.SuppressParent,
                IncludeType = __.IncludeType
            };

            await ProjectServices.References.AddOrUpdatePackageReferenceAsync(dependency, token);

            return true;
        }

        public override async Task AddFileToProjectAsync(string filePath)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            EnvDTEProjectUtility.EnsureCheckedOutIfExists(_vsProjectAdapter.Project, _vsProjectAdapter.ProjectDirectory, filePath);

            var isFileExistsInProject = await EnvDTEProjectUtility.ContainsFile(_vsProjectAdapter.Project, filePath);

            if (!isFileExistsInProject)
            {
                await AddProjectItemAsync(filePath);
            }
        }

        private async Task AddProjectItemAsync(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var folderPath = Path.GetDirectoryName(filePath);
            var fullPath = filePath;

            if (filePath.Contains(_vsProjectAdapter.ProjectDirectory))
            {
                // folderPath should always be relative to ProjectDirectory so if filePath already contains
                // ProjectDirectory then get a relative path and construct folderPath to get the appropriate
                // ProjectItems from dte where you have to add this file.
                var relativeLockFilePath = FileSystemUtility.GetRelativePath(_vsProjectAdapter.ProjectDirectory, filePath);
                folderPath = Path.GetDirectoryName(relativeLockFilePath);
            }
            else
            {
                // get the fullPath wrt ProjectDirectory
                fullPath = FileSystemUtility.GetFullPath(_vsProjectAdapter.ProjectDirectory, filePath);
            }

            var container = await EnvDTEProjectUtility.GetProjectItemsAsync(_vsProjectAdapter.Project, folderPath, createIfNotExists: true);

            container.AddFromFileCopy(fullPath);
        }

        public override async Task<bool> UninstallPackageAsync(
            PackageIdentity packageIdentity, INuGetProjectContext _, CancellationToken token)
        {
            await ProjectServices.References.RemovePackageReferenceAsync(packageIdentity.Id);

            return true;
        }

        #endregion

        private string GetMSBuildProjectExtensionsPath(bool shouldThrow = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var msbuildProjectExtensionsPath = _vsProjectAdapter.MSBuildProjectExtensionsPath;

            if (string.IsNullOrEmpty(msbuildProjectExtensionsPath))
            {
                if (shouldThrow)
                {
                    throw new InvalidDataException(string.Format(
                        Strings.MSBuildPropertyNotFound,
                        ProjectBuildProperties.MSBuildProjectExtensionsPath,
                        _vsProjectAdapter.ProjectDirectory));
                }

                return null;
            }

            return msbuildProjectExtensionsPath;
        }

        private string GetPackagesPath(ISettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var packagePath = _vsProjectAdapter.RestorePackagesPath;

            if (string.IsNullOrEmpty(packagePath))
            {
                return SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            return UriUtility.GetAbsolutePathFromFile(_projectFullPath, packagePath);
        }

        private IList<PackageSource> GetSources(ISettings settings, bool shouldThrow = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sources = MSBuildStringUtility.Split(_vsProjectAdapter.RestoreSources).AsEnumerable();

            if (ShouldReadFromSettings(sources))
            {
                sources = SettingsUtility.GetEnabledSources(settings).Select(e => e.Source);
            }
            else
            {
                sources = VSRestoreSettingsUtilities.HandleClear(sources);
            }

            // Add additional sources
            sources = sources.Concat(MSBuildStringUtility.Split(_vsProjectAdapter.RestoreAdditionalProjectSources));

            return sources.Select(e => new PackageSource(UriUtility.GetAbsolutePathFromFile(_projectFullPath, e))).ToList();
        }

        private IList<string> GetFallbackFolders(ISettings settings, bool shouldThrow = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fallbackFolders = MSBuildStringUtility.Split(_vsProjectAdapter.RestoreFallbackFolders).AsEnumerable();

            if (ShouldReadFromSettings(fallbackFolders))
            {
                fallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings);
            }
            else
            {
                fallbackFolders = VSRestoreSettingsUtilities.HandleClear(fallbackFolders);
            }

            // Add additional fallback folders
            fallbackFolders = fallbackFolders.Concat(MSBuildStringUtility.Split(_vsProjectAdapter.RestoreAdditionalProjectFallbackFolders));

            return fallbackFolders.Select(e => UriUtility.GetAbsolutePathFromFile(_projectFullPath, e)).ToList();
        }

        private static bool ShouldReadFromSettings(IEnumerable<string> values)
        {
            return !values.Any();
        }

        private IList<string> GetConfigFilePaths(ISettings settings)
        {
            return settings.GetConfigFilePaths();
        }

        // NOTES: Original
        //private async Task<IEnumerable<PackageReference>> GetPackageReferencesAsync(PackageSpec packageSpec)
        //{
        //    var frameworkSorter = new NuGetFrameworkSorter();

        //    var assetsFilePath = await GetAssetsFilePathAsync();
        //    var fileInfo = new FileInfo(assetsFilePath);
        //    PackageSpec assetsPackageSpec = default;
        //    IList<LockFileTarget> targets = default;

        //    if (fileInfo.Exists && fileInfo.LastWriteTimeUtc > _lastTimeAssetsModified)
        //    {
        //        await TaskScheduler.Default;
        //        var lockFile = new LockFileFormat().Read(assetsFilePath);
        //        assetsPackageSpec = lockFile.PackageSpec;
        //        targets = lockFile.Targets;

        //        _lastTimeAssetsModified = fileInfo.LastWriteTimeUtc;
        //    }

        //    return packageSpec
        //       .TargetFrameworks
        //       .SelectMany(f => GetPackageReferences(f.Dependencies, f.FrameworkName, _installedPackages, assetsPackageSpec, targets))
        //       .GroupBy(p => p.PackageIdentity)
        //       .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First());
        //}

        private async Task<IEnumerable<PackageReference>> GetPackageReferencesAsync(PackageSpec packageSpec)
        {
            var frameworkSorter = new NuGetFrameworkSorter();

            var assetsFilePath = await GetAssetsFilePathAsync();
            var fileInfo = new FileInfo(assetsFilePath);
            PackageSpec assetsPackageSpec = default;
            IList<LockFileTarget> targets = default;

            if (fileInfo.Exists && fileInfo.LastWriteTimeUtc > _lastTimeAssetsModified)
            {
                await TaskScheduler.Default;
                var lockFile = new LockFileFormat().Read(assetsFilePath);
                assetsPackageSpec = lockFile.PackageSpec;
                targets = lockFile.Targets;

                _lastTimeAssetsModified = fileInfo.LastWriteTimeUtc;
            }

            var tfs = packageSpec.TargetFrameworks;

            var returnStuff =  packageSpec
               .TargetFrameworks
               .SelectMany(f => GetPackageReferences(f.Dependencies, f.FrameworkName, _installedPackages, assetsPackageSpec, targets))
               .GroupBy(p => p.PackageIdentity)
               .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First());

            return returnStuff;
        }

        //private IEnumerable<PackageReference> GetPackageReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework, Dictionary<string, ProjectInstalledPackage> installedPackages, PackageSpec assetsPackageSpec, IList<LockFileTarget> targets)
        //{
        //    return libraries
        //        .Where(library => library.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
        //        .Select(library => new BuildIntegratedPackageReference(library, targetFramework, GetPackageReferenceUtility.UpdateResolvedVersion(library, targetFramework, assetsPackageSpec?.TargetFrameworks.FirstOrDefault(), targets, installedPackages)));
        //}

        private IEnumerable<PackageReference> GetPackageReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework, Dictionary<string, ProjectInstalledPackage> installedPackages, PackageSpec assetsPackageSpec, IList<LockFileTarget> targets)
        {
            System.Diagnostics.Debug.Print("-- in GetPackageReferences --");

            //Dictionary<string, VersionRange> dependentPackages = new Dictionary<string, VersionRange>();
            //if (targets != null)
            //{
            //    foreach (LockFileTarget target in targets)
            //    {
            //        foreach (LockFileTargetLibrary lib in target.Libraries)
            //        {
            //            foreach (PackageDependency dep in lib.Dependencies)
            //            {
            //                dependentPackages[dep.Id] = dep.VersionRange;
            //            }
            //        }
            //    }
            //}

            Dictionary<string, ProjectInstalledPackage> transitivePackages = new Dictionary<string, ProjectInstalledPackage>();
            if (targets != null)
            {
                System.Diagnostics.Debug.Print("-- in targets --");
                var transitive = targets
                .SelectMany(t => t.Libraries)
                .Where(l => l.Type == LibraryType.Package)
                .SelectMany(l => l.Dependencies)
                .Distinct()
                .Select(library => GetPackageReferenceUtility.UpdateResolvedVersionTransitive(library, targetFramework, assetsPackageSpec?.TargetFrameworks.FirstOrDefault(), targets, transitivePackages))
                .Select(pkgid => new PackageReference(pkgid, targetFramework));

                //.Select(library => new PackageReference(GetPackageReferenceUtility.UpdateResolvedVersionTransitive(library, targetFramework, assetsPackageSpec?.TargetFrameworks.FirstOrDefault(), targets, transitivePackages), targetFramework));
            }

            //                .Where(l => l.Type == LibraryType.Package);

            //                .Select(library => new BuildIntegratedPackageReference(library, targetFramework, GetPackageReferenceUtility.UpdateResolvedVersion(library, targetFramework, assetsPackageSpec?.TargetFrameworks.FirstOrDefault(), targets, transitivedPackages)));

            var libs = libraries
                .Where(library => library.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(library => new BuildIntegratedPackageReference(library, targetFramework, GetPackageReferenceUtility.UpdateResolvedVersion(library, targetFramework, assetsPackageSpec?.TargetFrameworks.FirstOrDefault(), targets, installedPackages)));

            return libs;
        }

        /// <summary>
        /// Emulates a JSON deserialization from project.json to PackageSpec in a post-project.json world
        /// </summary>
        private async Task<PackageSpec> GetPackageSpecAsync(ISettings settings)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectReferences = await ProjectServices
                .ReferencesReader
                .GetProjectReferencesAsync(Common.NullLogger.Instance, CancellationToken.None);

            var targetFramework = await _vsProjectAdapter.GetTargetFrameworkAsync();

            var packageReferences = (await ProjectServices
                .ReferencesReader
                .GetPackageReferencesAsync(targetFramework, CancellationToken.None))
                .ToList();

            var packageTargetFallback = MSBuildStringUtility.Split(_vsProjectAdapter.PackageTargetFallback)
                .Select(NuGetFramework.Parse)
                .ToList();

            var assetTargetFallback = MSBuildStringUtility.Split(_vsProjectAdapter.AssetTargetFallback)
                .Select(NuGetFramework.Parse)
                .ToList();

            var projectTfi = new TargetFrameworkInformation
            {
                FrameworkName = targetFramework,
                Dependencies = packageReferences,
            };

            bool isCpvmEnabled = await IsCentralPackageManagementVersionsEnabledAsync();
            if (isCpvmEnabled)
            {
                projectTfi.CentralPackageVersions.AddRange(await GetCentralPackageVersionsAsync());
            }

            // Apply fallback settings
            AssetTargetFallbackUtility.ApplyFramework(projectTfi, packageTargetFallback, assetTargetFallback);

            // Build up runtime information.
            var runtimes = await _vsProjectAdapter.GetRuntimeIdentifiersAsync();
            var supports = await _vsProjectAdapter.GetRuntimeSupportsAsync();
            var runtimeGraph = new RuntimeGraph(runtimes, supports);

            // In legacy CSProj, we only have one target framework per project
            var tfis = new TargetFrameworkInformation[] { projectTfi };

            var projectName = _projectName ?? _projectUniqueName;

            return new PackageSpec(tfis)
            {
                Name = projectName,
                Version = new NuGetVersion(_vsProjectAdapter.Version),
                Authors = new string[] { },
                Owners = new string[] { },
                Tags = new string[] { },
                ContentFiles = new string[] { },
                FilePath = _projectFullPath,
                RuntimeGraph = runtimeGraph,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    OutputPath = GetMSBuildProjectExtensionsPath(),
                    ProjectPath = _projectFullPath,
                    ProjectName = projectName,
                    ProjectUniqueName = _projectFullPath,
                    OriginalTargetFrameworks = tfis
                        .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                        .ToList(),
                    TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>
                    {
                        new ProjectRestoreMetadataFrameworkInfo(tfis[0].FrameworkName)
                        {
                            ProjectReferences = projectReferences?.ToList()
                        }
                    },
                    SkipContentFileWrite = true,
                    CacheFilePath = await GetCacheFilePathAsync(),
                    PackagesPath = GetPackagesPath(settings),
                    Sources = GetSources(settings),
                    FallbackFolders = GetFallbackFolders(settings),
                    ConfigFilePaths = GetConfigFilePaths(settings),
                    ProjectWideWarningProperties = WarningProperties.GetWarningProperties(
                        treatWarningsAsErrors: _vsProjectAdapter.TreatWarningsAsErrors,
                        noWarn: _vsProjectAdapter.NoWarn,
                        warningsAsErrors: _vsProjectAdapter.WarningsAsErrors),
                    RestoreLockProperties = new RestoreLockProperties(
                        await _vsProjectAdapter.GetRestorePackagesWithLockFileAsync(),
                        await _vsProjectAdapter.GetNuGetLockFilePathAsync(),
                        await _vsProjectAdapter.IsRestoreLockedAsync()),
                    CentralPackageVersionsEnabled = isCpvmEnabled
                }
            };
        }
    }
}
