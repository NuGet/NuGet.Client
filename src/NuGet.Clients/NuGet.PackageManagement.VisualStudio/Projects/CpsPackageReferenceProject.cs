// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.VisualStudio;
using PackageReference = NuGet.Packaging.PackageReference;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a project object associated with Common Project System (CPS) project that has opt'd
    /// into package references. This includes, but may not be limited to, .NET Project System,
    /// C++/CLI (with PackageReference support) and MSIX deployment projects.
    /// Key feature/difference is the project restore info is pushed by nomination API and stored in 
    /// a cache. Factory method retrieving the info from the cache should be provided.
    /// </summary>
    public class CpsPackageReferenceProject : PackageReferenceProject
    {
        private const string TargetFrameworkCondition = "TargetFramework";

        private readonly IProjectSystemCache _projectSystemCache;
        private readonly UnconfiguredProject _unconfiguredProject;
        private List<(NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages)> _installedPackages = new List<(NuGetFramework, Dictionary<string, ProjectInstalledPackage>)>();
        private List<(NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages)> _transitivePackages = new List<(NuGetFramework, Dictionary<string, ProjectInstalledPackage>)>();
        private WeakReference<PackageSpec> _lastPackageSpec;

        public CpsPackageReferenceProject(
            string projectName,
            string projectUniqueName,
            string projectFullPath,
            IProjectSystemCache projectSystemCache,
            UnconfiguredProject unconfiguredProject,
            INuGetProjectServices projectServices,
            string projectId)
            : base(projectName,
                projectUniqueName,
                projectFullPath)
        {
            Assumes.Present(projectFullPath);
            Assumes.Present(projectSystemCache);
            Assumes.Present(projectServices);

            ProjectStyle = ProjectStyle.PackageReference;

            _projectSystemCache = projectSystemCache;
            _unconfiguredProject = unconfiguredProject;
            ProjectServices = projectServices;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);
        }


        public override Task AddFileToProjectAsync(string filePath)
        {
            // sdk-style project system uses globbing to dynamically add files from project root into project
            // so we dont need to do anything explicitly here.
            return Task.CompletedTask;
        }

        private protected override Task<string> GetAssetsFilePathAsync(bool shouldThrow)
        {
            var packageSpec = GetPackageSpec();
            if (packageSpec == null)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException(
                        string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
                }
                else
                {
                    return Task.FromResult<string>(null);
                }
            }

            return Task.FromResult(Path.Combine(
                packageSpec.RestoreMetadata.OutputPath,
                LockFileFormat.AssetsFileName));
        }

        private PackageSpec GetPackageSpec()
        {
            DependencyGraphSpec projectRestoreInfo;
            if (_projectSystemCache.TryGetProjectRestoreInfo(_projectFullPath, out projectRestoreInfo, out _))
            {
                return projectRestoreInfo.GetProjectSpec(_projectFullPath);
            }

            // if restore data was not found in the cache, meaning project nomination
            // didn't happen yet or failed.
            return null;
        }


        #region IDependencyGraphProject


        public override string MSBuildProjectPath => _projectFullPath;

        public override Task<(IReadOnlyList<PackageSpec> dgSpecs, IReadOnlyList<IAssetsLogMessage> additionalMessages)> GetPackageSpecsAndAdditionalMessagesAsync(DependencyGraphCacheContext context)
        {
            var projects = new List<PackageSpec>();

            DependencyGraphSpec projectRestoreInfo;
            IReadOnlyList<IAssetsLogMessage> additionalMessages;
            if (!_projectSystemCache.TryGetProjectRestoreInfo(_projectFullPath, out projectRestoreInfo, out additionalMessages))
            {
                throw new InvalidOperationException(
                    string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
            }

            // Apply ISettings when needed to the return values.
            // This should not change the cached specs since they
            // contain values such as CLEAR which need to be persisted
            // and used here.
            var originalProjects = projectRestoreInfo.Projects;

            var settings = context?.Settings ?? NullSettings.Instance;

            foreach (var originalProject in originalProjects)
            {
                var project = originalProject.Clone();

                // Read restore settings from ISettings if it doesn't exist in the project
                // NOTE: Very important that the original project is used in the arguments, because cloning sorts the sources and compromises how the sources will be evaluated
                project.RestoreMetadata.PackagesPath = VSRestoreSettingsUtilities.GetPackagesPath(settings, originalProject);
                project.RestoreMetadata.Sources = VSRestoreSettingsUtilities.GetSources(settings, originalProject);
                project.RestoreMetadata.FallbackFolders = VSRestoreSettingsUtilities.GetFallbackFolders(settings, originalProject);
                project.RestoreMetadata.ConfigFilePaths = GetConfigFilePaths(settings);
                IgnoreUnsupportProjectReference(project);
                projects.Add(project);
            }

            if (context != null)
            {
                PackageSpec ignore;
                foreach (var project in projects
                    .Where(p => !context.PackageSpecCache.TryGetValue(
                        p.RestoreMetadata.ProjectUniqueName, out ignore)))
                {
                    context.PackageSpecCache.Add(
                        project.RestoreMetadata.ProjectUniqueName,
                        project);
                }
            }

            return Task.FromResult<(IReadOnlyList<PackageSpec>, IReadOnlyList<IAssetsLogMessage>)>((projects, additionalMessages));
        }

        private IList<string> GetConfigFilePaths(ISettings settings)
        {
            return settings.GetConfigFilePaths();
        }

        private void IgnoreUnsupportProjectReference(PackageSpec project)
        {
            foreach (var frameworkInfo in project.RestoreMetadata.TargetFrameworks)
            {
                var projectReferences = new List<ProjectRestoreReference>();

                foreach (var projectReference in frameworkInfo.ProjectReferences)
                {
                    if (ProjectType.IsSupportedProjectExtension(projectReference.ProjectPath))
                    {
                        projectReferences.Add(projectReference);
                    }
                }

                frameworkInfo.ProjectReferences = projectReferences;
            }
        }

        #endregion

        #region NuGetProject

        /// <summary>
        /// Gets the installed (top level) package references for this project. 
        /// </summary>
        public async override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            ProjectPackages packages = await GetInstalledAndTransitivePackagesAsync(token);
            return packages.InstalledPackages;
        }

        /// <summary>
        /// Gets the both the installed (top level) and transitive package references for this project.
        /// Returns the package reference as two separate lists (installed and transitive).
        /// </summary>
        public override async Task<ProjectPackages> GetInstalledAndTransitivePackagesAsync(CancellationToken token)
        {
            var packageSpec = GetPackageSpec();

            if (packageSpec != null)
            {
                var frameworkSorter = new NuGetFrameworkSorter();

                string assetsFilePath = await GetAssetsFilePathAsync();
                var fileInfo = new FileInfo(assetsFilePath);
                IList<LockFileTarget> targets = default;
                PackageSpec lastPackageSpec = default;
                _lastPackageSpec?.TryGetTarget(out lastPackageSpec);

                if ((fileInfo.Exists && fileInfo.LastWriteTimeUtc > _lastTimeAssetsModified) || !ReferenceEquals(lastPackageSpec, packageSpec))
                {
                    await TaskScheduler.Default;
                    if (fileInfo.Exists)
                    {
                        var lockFile = LockFileUtilities.GetLockFile(assetsFilePath, NullLogger.Instance);
                        if (!(lockFile is null))
                        {
                            targets = lockFile.Targets;
                        }
                    }
                    _lastTimeAssetsModified = fileInfo.LastWriteTimeUtc;
                    _lastPackageSpec = new WeakReference<PackageSpec>(packageSpec);

                    // clear the transitive packages cache, since we don't know when a dependency has been removed
                    _transitivePackages = new List<(NuGetFramework, Dictionary<string, ProjectInstalledPackage>)>();
                }

                List<PackageReference> installedPackages = packageSpec
                   .TargetFrameworks
                   .SelectMany(f => GetPackageReferencesForFramework(f.Dependencies, f.FrameworkName, _installedPackages, targets))
                   .GroupBy(p => p.PackageIdentity)
                   .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                   .ToList();

                // get the transitive packages, excluding any already contained in the installed packages
                List<PackageReference> transitivePackages = packageSpec
                   .TargetFrameworks
                   .SelectMany(f => GetTransitivePackageReferencesForFramework(f.FrameworkName, _installedPackages, _transitivePackages, targets))
                   .GroupBy(p => p.PackageIdentity)
                   .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                   .ToList();
                return new ProjectPackages(installedPackages.ToList(), transitivePackages.ToList());
            }
            else
            {
                return new ProjectPackages(Array.Empty<PackageReference>(), Array.Empty<PackageReference>());
            }
        }

        private IEnumerable<PackageReference> GetPackageReferencesForFramework(
            IEnumerable<LibraryDependency> libraries,
            NuGetFramework targetFramework,
            List<(NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages)> installedPackages,
            IList<LockFileTarget> targets)
        {
            (NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages) targetFrameworkPackages = installedPackages.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework));

            if (targetFrameworkPackages.Packages == null)
            {
                targetFrameworkPackages.TargetFramework = targetFramework;
                targetFrameworkPackages.Packages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
                installedPackages.Add(targetFrameworkPackages);
            }

            return GetPackageReferences(libraries, targetFramework, targetFrameworkPackages.Packages, targets);
        }

        private IReadOnlyList<PackageReference> GetTransitivePackageReferencesForFramework(
            NuGetFramework targetFramework,
            List<(NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages)> installedPackages,
            List<(NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages)> transitivePackages,
            IList<LockFileTarget> targets)
        {
            (NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages) targetFrameworkInstalledPackages = installedPackages.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework));
            (NuGetFramework TargetFramework, Dictionary<string, ProjectInstalledPackage> Packages) targetFrameworkTransitivePackages = transitivePackages.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework));

            if (targetFrameworkInstalledPackages.Packages == null)
            {
                targetFrameworkInstalledPackages.TargetFramework = targetFramework;
                targetFrameworkInstalledPackages.Packages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
                installedPackages.Add(targetFrameworkInstalledPackages);
            }

            if (targetFrameworkTransitivePackages.Packages == null)
            {
                targetFrameworkTransitivePackages.TargetFramework = targetFramework;
                targetFrameworkTransitivePackages.Packages = new Dictionary<string, ProjectInstalledPackage>(StringComparer.OrdinalIgnoreCase);
                transitivePackages.Add(targetFrameworkTransitivePackages);
            }

            return GetTransitivePackageReferences(targetFramework, targetFrameworkInstalledPackages.Packages, targetFrameworkTransitivePackages.Packages, targets);
        }

        public override async Task<bool> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext nuGetProjectContext,
            BuildIntegratedInstallationContext installationContext,
            CancellationToken token)
        {
            // Right now, the UI only handles installation of specific versions, which is just the minimum version of
            // the provided version range.
            var formattedRange = range.MinVersion.ToNormalizedString();

            nuGetProjectContext.Log(MessageLevel.Info, Strings.InstallingPackage, $"{packageId} {formattedRange}");

            if (installationContext.SuccessfulFrameworks.Any() && installationContext.UnsuccessfulFrameworks.Any())
            {
                // This is the "partial install" case. That is, install the package to only a subset of the frameworks
                // supported by this project.
                var conditionalService = _unconfiguredProject
                    .Services
                    .ExportProvider
                    .GetExportedValue<IConditionalPackageReferencesService>();

                if (conditionalService == null)
                {
                    throw new InvalidOperationException(string.Format(
                        Strings.UnableToGetCPSPackageInstallationService,
                        _projectFullPath));
                }

                foreach (var framework in installationContext.SuccessfulFrameworks)
                {
                    string originalFramework;
                    if (!installationContext.OriginalFrameworks.TryGetValue(framework, out originalFramework))
                    {
                        originalFramework = framework.GetShortFolderName();
                    }

                    var reference = await conditionalService.AddAsync(
                        packageId,
                        formattedRange,
                        TargetFrameworkCondition,
                        originalFramework);

                    // SuppressParent could be set to All if developmentDependency flag is true in package nuspec file.
                    if (installationContext.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent &&
                        installationContext.IncludeType != LibraryIncludeFlags.All)
                    {
                        await SetPackagePropertyValueAsync(
                            reference.Metadata,
                            ProjectItemProperties.PrivateAssets,
                            MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(installationContext.SuppressParent)));

                        await SetPackagePropertyValueAsync(
                            reference.Metadata,
                            ProjectItemProperties.IncludeAssets,
                            MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(installationContext.IncludeType)));
                    }
                }
            }
            else
            {
                // Install the package to all frameworks.
                var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();

                var result = await configuredProject
                    .Services
                    .PackageReferences
                    .AddAsync(packageId, formattedRange);

                // This is the update operation
                if (!result.Added)
                {
                    var existingReference = result.Reference;
                    await existingReference.Metadata.SetPropertyValueAsync("Version", formattedRange);
                }

                if (installationContext.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent &&
                    installationContext.IncludeType != LibraryIncludeFlags.All)
                {
                    await SetPackagePropertyValueAsync(
                        result.Reference.Metadata,
                        ProjectItemProperties.PrivateAssets,
                        MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(installationContext.SuppressParent)));

                    await SetPackagePropertyValueAsync(
                        result.Reference.Metadata,
                        ProjectItemProperties.IncludeAssets,
                        MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(installationContext.IncludeType)));
                }
            }

            return true;
        }

        private async Task SetPackagePropertyValueAsync(IProjectProperties metadata, string propertyName, string propertyValue)
        {
            await metadata.SetPropertyValueAsync(
                propertyName,
                propertyValue);
        }

        public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            await configuredProject?.Services.PackageReferences.RemoveAsync(packageIdentity.Id);
            return true;
        }

        public override Task<string> GetCacheFilePathAsync()
        {
            var spec = GetPackageSpec();
            if (spec == null)
            {
                throw new InvalidOperationException(
                    string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
            }

            return Task.FromResult(NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: spec.RestoreMetadata.OutputPath));
        }

        #endregion
    }
}
