// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.RuntimeModel;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [DebuggerDisplay("{ProjectName}")]
    internal class VsProjectAdapter : IVsProjectAdapter
    {
        #region Private members

        private readonly VsHierarchy _vsHierarchy;
        private readonly Lazy<EnvDTE.Project> _dteProject;
        private readonly IDeferredProjectWorkspaceService _workspaceService;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly string _projectTypeGuid;

        #endregion Private members

        #region Properties

        public async Task<string> GetMSBuildProjectExtensionsPathAsync()
        {
            var msbuildProjectExtensionsPath = BuildProperties.GetPropertyValue(ProjectBuildProperties.MSBuildProjectExtensionsPath);

            if (string.IsNullOrEmpty(msbuildProjectExtensionsPath))
            {
                return null;
            }

            return Path.Combine(await GetProjectDirectoryAsync(), msbuildProjectExtensionsPath);
        }

        public string RestorePackagesPath
        {
            get
            {
                var restorePackagesPath = BuildProperties.GetPropertyValue(ProjectBuildProperties.RestorePackagesPath);

                if (string.IsNullOrWhiteSpace(restorePackagesPath))
                {
                    return null;
                }

                return restorePackagesPath;
            }
        }

        public string RestoreSources
        {
            get
            {
                var restoreSources = BuildProperties.GetPropertyValue(ProjectBuildProperties.RestoreSources);

                if (string.IsNullOrWhiteSpace(restoreSources))
                {
                    return null;
                }

                return restoreSources;
            }
        }

        public string RestoreFallbackFolders
        {
            get
            {
                var restoreFallbackFolders = BuildProperties.GetPropertyValue(ProjectBuildProperties.RestoreFallbackFolders);

                if (string.IsNullOrWhiteSpace(restoreFallbackFolders))
                {
                    return null;
                }

                return restoreFallbackFolders;
            }
        }

        public IProjectBuildProperties BuildProperties { get; private set; }

        public string CustomUniqueName => ProjectNames.CustomUniqueName;

        public string FullName => ProjectNames.FullName;

        public async Task<string> GetProjectDirectoryAsync()
        {
            if (!IsDeferred)
            {
                return await EnvDTEProjectInfoUtility.GetFullPathAsync(Project);
            }
            else
            {
                return Path.GetDirectoryName(FullProjectPath);
            }
        }

        public string FullProjectPath { get; private set; }

        public bool IsDeferred
        {
            get
            {
                return false;
            }
        }

        public async Task<bool> IsSupportedAsync()
        {
            if (!IsDeferred)
            {
                return EnvDTEProjectUtility.IsSupported(Project);
            }

            return await VsHierarchy.IsSupportedAsync(_projectTypeGuid);
        }

        public string PackageTargetFallback
        {
            get
            {
                return BuildProperties.GetPropertyValue(ProjectBuildProperties.PackageTargetFallback);
            }
        }

        public string AssetTargetFallback
        {
            get
            {
                return BuildProperties.GetPropertyValue(ProjectBuildProperties.AssetTargetFallback);
            }
        }

        public EnvDTE.Project Project => _dteProject.Value;

        public async Task<string> GetProjectIdAsync()
        {
            return (await _vsHierarchy.GetProjectIdAsync()).ToString();
        }

        public string ProjectName => ProjectNames.ShortName;

        public ProjectNames ProjectNames { get; private set; }

        public string UniqueName => ProjectNames.UniqueName;

        public string Version
        {
            get
            {
                _threadingService.ThrowIfNotOnUIThread();

                var packageVersion = BuildProperties.GetPropertyValue(ProjectBuildProperties.PackageVersion);

                if (string.IsNullOrEmpty(packageVersion))
                {
                    packageVersion = BuildProperties.GetPropertyValue(ProjectBuildProperties.Version);

                    if (string.IsNullOrEmpty(packageVersion))
                    {
                        packageVersion = "1.0.0";
                    }
                }

                return packageVersion;
            }
        }

        public VsHierarchy VsHierarchy => _vsHierarchy;

        public string RestoreAdditionalProjectSources => BuildProperties.GetPropertyValue(ProjectBuildProperties.RestoreAdditionalProjectSources);

        public string RestoreAdditionalProjectFallbackFolders => BuildProperties.GetPropertyValue(ProjectBuildProperties.RestoreAdditionalProjectFallbackFolders);

        public string NoWarn => BuildProperties.GetPropertyValue(ProjectBuildProperties.NoWarn);

        public string WarningsAsErrors => BuildProperties.GetPropertyValue(ProjectBuildProperties.WarningsAsErrors);

        public string TreatWarningsAsErrors => BuildProperties.GetPropertyValue(ProjectBuildProperties.TreatWarningsAsErrors);

        #endregion Properties

        #region Constructors

        public VsProjectAdapter(
            VsHierarchy vsHierarchyItem,
            ProjectNames projectNames,
            string fullProjectPath,
            string projectTypeGuid,
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject,
            IProjectBuildProperties buildProperties,
            IVsProjectThreadingService threadingService,
            IDeferredProjectWorkspaceService workspaceService = null)
        {
            Assumes.Present(vsHierarchyItem);

            _vsHierarchy = vsHierarchyItem;
            _dteProject = new Lazy<EnvDTE.Project>(() => loadDteProject(_vsHierarchy.Ptr));
            _workspaceService = workspaceService;
            _threadingService = threadingService;
            _projectTypeGuid = projectTypeGuid;

            FullProjectPath = fullProjectPath;
            ProjectNames = projectNames;
            BuildProperties = buildProperties;
        }

        #endregion Constructors

        #region Getters

        public async Task<string[]> GetProjectTypeGuidsAsync()
        {
            if (!IsDeferred)
            {
                return await (await NuGet.VisualStudio.VsHierarchy.FromDteProjectAsync(Project)).GetProjectTypeGuidsAsync();
            }
            else
            {
                // Get ProjectTypeGuids from msbuild property, if it doesn't exist, fall back to projectTypeGuid.
                var projectTypeGuids = await BuildProperties.GetPropertyValueAsync(ProjectBuildProperties.ProjectTypeGuids);

                if (!string.IsNullOrEmpty(projectTypeGuids))
                {
                    return MSBuildStringUtility.Split(projectTypeGuids);
                }

                if (!string.IsNullOrEmpty(_projectTypeGuid))
                {
                    return new string[] { _projectTypeGuid };
                }

                return Array.Empty<string>();
            }
        }

        public async Task<FrameworkName> GetDotNetFrameworkNameAsync()
        {
            var targetFrameworkMoniker = await GetTargetFrameworkStringAsync();

            if (!string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                return new FrameworkName(targetFrameworkMoniker);
            }

            return null;
        }

        public async Task<IEnumerable<string>> GetReferencedProjectsAsync()
        {
            if (!IsDeferred)
            {
                if (Project.Kind != null
                    && SupportedProjectTypes.IsSupportedForAddingReferences(Project.Kind))
                {
                    return EnvDTEProjectUtility.GetReferencedProjects(Project).Select(p => p.UniqueName);
                }

                return Enumerable.Empty<string>();
            }
            else
            {
                var projectTypeGuids = await GetProjectTypeGuidsAsync();
                if (projectTypeGuids.All(SupportedProjectTypes.IsSupportedForAddingReferences))
                {
                    return await _workspaceService.GetProjectReferencesAsync(FullProjectPath);
                }

                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<RuntimeDescription>> GetRuntimeIdentifiersAsync()
        {
            _threadingService.ThrowIfNotOnUIThread();

            var unparsedRuntimeIdentifer = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.RuntimeIdentifier);
            var unparsedRuntimeIdentifers = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.RuntimeIdentifiers);

            var runtimes = Enumerable.Empty<string>();

            if (unparsedRuntimeIdentifer != null)
            {
                runtimes = runtimes.Concat(new[] { unparsedRuntimeIdentifer });
            }

            if (unparsedRuntimeIdentifers != null)
            {
                runtimes = runtimes.Concat(unparsedRuntimeIdentifers.Split(';'));
            }

            runtimes = runtimes
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x));

            return runtimes
                .Select(runtime => new RuntimeDescription(runtime));
        }

        public async Task<IEnumerable<CompatibilityProfile>> GetRuntimeSupportsAsync()
        {
            _threadingService.ThrowIfNotOnUIThread();

            var unparsedRuntimeSupports = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.RuntimeSupports);

            if (unparsedRuntimeSupports == null)
            {
                return Enumerable.Empty<CompatibilityProfile>();
            }

            return unparsedRuntimeSupports
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(support => new CompatibilityProfile(support));
        }

        public async Task<NuGetFramework> GetTargetFrameworkAsync()
        {
            var frameworkString = await GetTargetFrameworkStringAsync();

            if (!string.IsNullOrEmpty(frameworkString))
            {
                return NuGetFramework.Parse(frameworkString);
            }

            return NuGetFramework.UnsupportedFramework;
        }

        public Task<string> GetRestorePackagesWithLockFileAsync()
        {
            return GetPropertyValueAsync(ProjectBuildProperties.RestorePackagesWithLockFile);
        }

        public Task<string> GetNuGetLockFilePathAsync()
        {
            return GetPropertyValueAsync(ProjectBuildProperties.NuGetLockFilePath);
        }

        public async Task<bool> IsRestoreLockedAsync()
        {
            var value = await GetPropertyValueAsync(ProjectBuildProperties.RestoreLockedMode);

            return MSBuildStringUtility.IsTrue(value);
        }

        public async Task<string> GetPropertyValueAsync(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            return await BuildProperties.GetPropertyValueAsync(propertyName);
        }

        public async Task<IEnumerable<(string ItemId, string[] ItemMetadata)>> GetBuildItemInformationAsync(string itemName, params string[] metadataNames)
        {
            if (itemName == null)
            {
                throw new ArgumentNullException(nameof(itemName));
            }
            if (metadataNames == null)
            {
                throw new ArgumentNullException(nameof(itemName));
            }

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var itemStorage = VsHierarchy.Ptr as IVsBuildItemStorage;
            if (itemStorage != null)
            {
                var callback = new VisualStudioBuildItemStorageCallback();
                itemStorage.FindItems(itemName, metadataNames.Length, metadataNames, callback);

                return callback.Items;
            }

            return Enumerable.Empty<(string ItemId, string[] ItemMetadata)>();
        }

        private async Task<string> GetTargetFrameworkStringAsync()
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectPath = FullName;
            var platformIdentifier = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.TargetPlatformIdentifier);
            var platformVersion = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.TargetPlatformVersion);
            var platformMinVersion = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.TargetPlatformMinVersion);
            var targetFrameworkMoniker = await BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.TargetFrameworkMoniker);

            // Projects supporting TargetFramework and TargetFrameworks are detected before
            // this check. The values can be passed as null here.
            var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                projectFilePath: projectPath,
                targetFrameworks: null,
                targetFramework: null,
                targetFrameworkMoniker: targetFrameworkMoniker,
                targetPlatformIdentifier: platformIdentifier,
                targetPlatformVersion: platformVersion,
                targetPlatformMinVersion: platformMinVersion);

            return frameworkStrings.FirstOrDefault();
        }

        #endregion Getters
    }
}
