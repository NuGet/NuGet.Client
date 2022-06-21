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
using Microsoft.VisualStudio.Shell;
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

        private readonly VsHierarchyItem _vsHierarchyItem;
        private readonly Lazy<EnvDTE.Project> _dteProject;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly string _projectTypeGuid;

        #endregion Private members

        #region Properties

        public async Task<string> GetMSBuildProjectExtensionsPathAsync()
        {
            var msbuildProjectExtensionsPath = await BuildProperties.GetPropertyValueAsync(ProjectBuildProperties.MSBuildProjectExtensionsPath);

            if (string.IsNullOrEmpty(msbuildProjectExtensionsPath))
            {
                return null;
            }

            return Path.Combine(await GetProjectDirectoryAsync(), msbuildProjectExtensionsPath);
        }

        public IProjectBuildProperties BuildProperties { get; private set; }

        public string CustomUniqueName => ProjectNames.CustomUniqueName;

        public string FullName => ProjectNames.FullName;

        public async Task<string> GetProjectDirectoryAsync()
        {
            return await Project.GetFullPathAsync();
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
            return await EnvDTEProjectUtility.IsSupportedAsync(Project);
        }

        public EnvDTE.Project Project => _dteProject.Value;

        public string ProjectId
        {
            get
            {
                Guid id;
                if (!_vsHierarchyItem.TryGetProjectId(out id))
                {
                    id = Guid.Empty;
                }

                return id.ToString();
            }
        }

        public string ProjectName => ProjectNames.ShortName;

        public ProjectNames ProjectNames { get; private set; }

        public string UniqueName => ProjectNames.UniqueName;

        public string Version
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

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

        public IVsHierarchy VsHierarchy => _vsHierarchyItem.VsHierarchy;

        #endregion Properties

        #region Constructors

        public VsProjectAdapter(
            VsHierarchyItem vsHierarchyItem,
            ProjectNames projectNames,
            string fullProjectPath,
            string projectTypeGuid,
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject,
            IProjectBuildProperties buildProperties,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsHierarchyItem);

            _vsHierarchyItem = vsHierarchyItem;
            _dteProject = new Lazy<EnvDTE.Project>(() => loadDteProject(_vsHierarchyItem.VsHierarchy));
            _threadingService = threadingService;
            _projectTypeGuid = projectTypeGuid;

            FullProjectPath = fullProjectPath;
            ProjectNames = projectNames;
            BuildProperties = buildProperties;
        }

        #endregion Constructors

        #region Getters

        public Task<string[]> GetProjectTypeGuidsAsync()
        {
            return Project.GetProjectTypeGuidsAsync();
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
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Project.Kind != null
                && ProjectType.IsSupportedForAddingReferences(Project.Kind))
            {
                return EnvDTEProjectUtility.GetReferencedProjects(Project)
                    .Select(p =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        return p.UniqueName;
                    });
            }

            return Enumerable.Empty<string>();
        }

        public async Task<IEnumerable<RuntimeDescription>> GetRuntimeIdentifiersAsync()
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var unparsedRuntimeIdentifer = BuildProperties.GetPropertyValue(
                ProjectBuildProperties.RuntimeIdentifier);
            var unparsedRuntimeIdentifers = BuildProperties.GetPropertyValue(
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
                .Distinct(StringComparer.Ordinal)
                .Where(x => !string.IsNullOrEmpty(x));

            return runtimes
                .Select(runtime => new RuntimeDescription(runtime));
        }

        public async Task<IEnumerable<CompatibilityProfile>> GetRuntimeSupportsAsync()
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var unparsedRuntimeSupports = BuildProperties.GetPropertyValue(
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

        public Task<string> GetPropertyValueAsync(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            return BuildProperties.GetPropertyValueAsync(propertyName);
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

            var itemStorage = VsHierarchy as IVsBuildItemStorage;
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
            var platformIdentifier = BuildProperties.GetPropertyValue(
                ProjectBuildProperties.TargetPlatformIdentifier);
            var platformVersion = BuildProperties.GetPropertyValue(
                ProjectBuildProperties.TargetPlatformVersion);
            var platformMinVersion = BuildProperties.GetPropertyValue(
                ProjectBuildProperties.TargetPlatformMinVersion);
            var targetFrameworkMoniker = BuildProperties.GetPropertyValue(
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

        public async Task<bool> IsCapabilityMatchAsync(string capabilityExpression)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            return VsHierarchy.IsCapabilityMatch(capabilityExpression);
        }

        #endregion Getters
    }
}
