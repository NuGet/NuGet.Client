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
    public sealed class LegacyPackageReferenceProject : PackageReferenceProject
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;

        // key is packageId
        private readonly Dictionary<string, ProjectInstalledPackage> _installedPackages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ProjectInstalledPackage> _transitivePackages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
        public NuGetFramework TargetFramework { get; }

        public LegacyPackageReferenceProject(
            IVsProjectAdapter vsProjectAdapter,
            string projectId,
            INuGetProjectServices projectServices,
            IVsProjectThreadingService threadingService)
            : base(vsProjectAdapter.ProjectName,
                vsProjectAdapter.UniqueName,
                vsProjectAdapter.FullProjectPath)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.NotNullOrEmpty(projectId);
            Assumes.Present(projectServices);
            Assumes.Present(threadingService);

            _vsProjectAdapter = vsProjectAdapter;
            _threadingService = threadingService;

            ProjectStyle = ProjectStyle.PackageReference;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, ProjectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, ProjectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, ProjectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);

            ProjectServices = projectServices;
        }

        public LegacyPackageReferenceProject(
            IVsProjectAdapter vsProjectAdapter,
            string projectId,
            INuGetProjectServices projectServices,
            IVsProjectThreadingService threadingService,
            NuGetFramework targetFramework)
            : this(vsProjectAdapter,
                projectId,
                projectServices,
                threadingService)
        {
            Assumes.NotNull(targetFramework);
            TargetFramework = targetFramework;
        }

        #region BuildIntegratedNuGetProject

        public override async Task<string> GetCacheFilePathAsync()
        {
            return NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: await GetMSBuildProjectExtensionsPathAsync());
        }

        protected override async Task<string> GetAssetsFilePathAsync(bool shouldThrow)
        {
            var msbuildProjectExtensionsPath = await GetMSBuildProjectExtensionsPathAsync(shouldThrow);
            if (msbuildProjectExtensionsPath == null)
            {
                return null;
            }

            return Path.Combine(msbuildProjectExtensionsPath, LockFileFormat.AssetsFileName);
        }

        #endregion BuildIntegratedNuGetProject

        #region IDependencyGraphProject

        public override string MSBuildProjectPath => ProjectFullPath;

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
                context?.PackageSpecCache.Add(ProjectFullPath, packageSpec);
            }

            return (new[] { packageSpec }, null);
        }

        private async Task<bool> IsCentralPackageManagementVersionsEnabledAsync()
        {
            return MSBuildStringUtility.IsTrue(await _vsProjectAdapter.GetPropertyValueAsync(ProjectBuildProperties.ManagePackageVersionsCentrally));
        }

        private async Task<string> GetSpecifiedAssemblyNameAsync()
        {
            return await _vsProjectAdapter.GetPropertyValueAsync(ProjectBuildProperties.AssemblyName);
        }

        private async Task<string> GetSpecifiedPackageIdAsync()
        {
            return await _vsProjectAdapter.GetPropertyValueAsync(ProjectBuildProperties.PackageId);
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

        /// <summary>
        /// Gets the installed (top level) package references for this project. 
        /// </summary>
        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            ProjectPackages packages = await GetInstalledAndTransitivePackagesAsync(token);
            return packages.InstalledPackages;
        }

        /// <inheritdoc/>
        public override async Task<ProjectPackages> GetInstalledAndTransitivePackagesAsync(CancellationToken token)
        {
            PackageSpec reading = await GetCachedPackageSpecAsync(token);
            if (reading == null)
            {
                IsInstalledAndTransitiveComputationNeeded = true;

                return new ProjectPackages(Array.Empty<PackageReference>(), Array.Empty<TransitivePackageReference>());
            }

            IList<LockFileTarget> targetsList = null;
            if (IsInstalledAndTransitiveComputationNeeded)
            {
                // clear the transitive packages cache, since we don't know when a dependency has been removed
                ClearCache();
                targetsList = await GetTargetsListAsync(token);
            }

            var frameworkSorter = new NuGetFrameworkSorter();

            // get the installed packages
            List<PackageReference> installedPackages = reading
               .TargetFrameworks
               .SelectMany(f => GetPackageReferences(
                   f.Dependencies,
                   f.FrameworkName,
                   _installedPackages,
                   targetsList))
               .GroupBy(p => p.PackageIdentity)
               .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
               .ToList();

            // get the transitive packages, excluding any already contained in the installed packages
            List<PackageReference> transitivePackages = reading
               .TargetFrameworks
               .SelectMany(f => GetTransitivePackageReferences(
                   f.FrameworkName,
                   _installedPackages,
                   _transitivePackages,
                   targetsList))
               .GroupBy(p => p.PackageIdentity)
               .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
               .ToList();

            List<TransitivePackageReference> transitivePackagesWithOrigins = transitivePackages
                .Select(pr => Tuple.Create(pr, GetTransitivePackageOrigin(
                    pr,
                    installedPackages,
                    targetsList,
                    token)))
                .Select(te => MergeTransitiveOrigin(te.Item1, te.Item2))
                .Where(transitivePR => transitivePR.TransitiveOrigins.Any())
                .ToList();

            IsInstalledAndTransitiveComputationNeeded = false;

            return new ProjectPackages(installedPackages, transitivePackagesWithOrigins);
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

            EnvDTEProjectUtility.EnsureCheckedOutIfExists(_vsProjectAdapter.Project, await _vsProjectAdapter.GetProjectDirectoryAsync(), filePath);

            var isFileExistsInProject = await EnvDTEProjectUtility.ContainsFileAsync(_vsProjectAdapter.Project, filePath);

            if (!isFileExistsInProject)
            {
                await AddProjectItemAsync(filePath);
            }
        }

        private async Task AddProjectItemAsync(string filePath)
        {
            var folderPath = Path.GetDirectoryName(filePath);
            var fullPath = filePath;

            string projectDirectory = await _vsProjectAdapter.GetProjectDirectoryAsync();
            if (filePath.Contains(projectDirectory))
            {
                // folderPath should always be relative to ProjectDirectory so if filePath already contains
                // ProjectDirectory then get a relative path and construct folderPath to get the appropriate
                // ProjectItems from dte where you have to add this file.
                var relativeLockFilePath = FileSystemUtility.GetRelativePath(projectDirectory, filePath);
                folderPath = Path.GetDirectoryName(relativeLockFilePath);
            }
            else
            {
                // get the fullPath wrt ProjectDirectory
                fullPath = FileSystemUtility.GetFullPath(projectDirectory, filePath);
            }

            var container = await EnvDTEProjectUtility.GetProjectItemsAsync(_vsProjectAdapter.Project, folderPath, createIfNotExists: true);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            container.AddFromFileCopy(fullPath);
        }

        public override async Task<bool> UninstallPackageAsync(
            PackageIdentity packageIdentity, INuGetProjectContext _, CancellationToken token)
        {
            await ProjectServices.References.RemovePackageReferenceAsync(packageIdentity.Id);

            return true;
        }

        #endregion

        private async Task<string> GetMSBuildProjectExtensionsPathAsync(bool shouldThrow = true)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var msbuildProjectExtensionsPath = await _vsProjectAdapter.GetMSBuildProjectExtensionsPathAsync();

            if (string.IsNullOrEmpty(msbuildProjectExtensionsPath))
            {
                if (shouldThrow)
                {
                    throw new InvalidDataException(string.Format(
                        Strings.MSBuildPropertyNotFound,
                        ProjectBuildProperties.MSBuildProjectExtensionsPath,
                        await _vsProjectAdapter.GetProjectDirectoryAsync()));
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

            return UriUtility.GetAbsolutePathFromFile(ProjectFullPath, packagePath);
        }

        private IList<PackageSource> GetSources(ISettings settings)
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

            return sources.Select(e => new PackageSource(UriUtility.GetAbsolutePathFromFile(ProjectFullPath, e))).ToList();
        }

        private IList<string> GetFallbackFolders(ISettings settings)
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

            return fallbackFolders.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectFullPath, e)).ToList();
        }

        private static bool ShouldReadFromSettings(IEnumerable<string> values)
        {
            return !values.Any();
        }

        private IList<string> GetConfigFilePaths(ISettings settings)
        {
            return settings.GetConfigFilePaths();
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
                // Add the central version information and merge the information to the package reference dependencies
                projectTfi.CentralPackageVersions.AddRange(await GetCentralPackageVersionsAsync());
                LibraryDependency.ApplyCentralVersionInformation(projectTfi.Dependencies, projectTfi.CentralPackageVersions);
            }

            // Apply fallback settings
            AssetTargetFallbackUtility.ApplyFramework(projectTfi, packageTargetFallback, assetTargetFallback);

            // Build up runtime information.
            var runtimes = await _vsProjectAdapter.GetRuntimeIdentifiersAsync();
            var supports = await _vsProjectAdapter.GetRuntimeSupportsAsync();
            var runtimeGraph = new RuntimeGraph(runtimes, supports);

            // In legacy CSProj, we only have one target framework per project
            var tfis = new TargetFrameworkInformation[] { projectTfi };

            var projectName = ProjectName ?? ProjectUniqueName;

            string specifiedPackageId = await GetSpecifiedPackageIdAsync();

            if (!string.IsNullOrWhiteSpace(specifiedPackageId))
            {
                projectName = specifiedPackageId;
            }
            else
            {
                string specifiedAssemblyName = await GetSpecifiedAssemblyNameAsync();

                if (!string.IsNullOrWhiteSpace(specifiedAssemblyName))
                {
                    projectName = specifiedAssemblyName;
                }
            }

            return new PackageSpec(tfis)
            {
                Name = projectName,
                Version = new NuGetVersion(_vsProjectAdapter.Version),
                FilePath = ProjectFullPath,
                RuntimeGraph = runtimeGraph,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    OutputPath = await GetMSBuildProjectExtensionsPathAsync(),
                    ProjectPath = ProjectFullPath,
                    ProjectName = projectName,
                    ProjectUniqueName = ProjectFullPath,
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

        /// <inheritdoc/>
        internal override ValueTask<PackageSpec> GetPackageSpecAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return new ValueTask<PackageSpec>(GetPackageSpecAsync(NullSettings.Instance));
        }

        /// <inheritdoc/>
        internal override bool IsPackageSpecDifferent(PackageSpec actual, PackageSpec cached)
        {
            return false; // PackageSpec is always created in LegacyPackageReference implementation
        }

        /// <inheritdoc/>
        internal override void ClearCache()
        {
            _installedPackages.Clear();
            _transitivePackages.Clear();
        }
    }
}
