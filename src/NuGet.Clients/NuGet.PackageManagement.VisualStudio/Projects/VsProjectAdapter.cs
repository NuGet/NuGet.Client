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
using NuGet.Frameworks;
using NuGet.ProjectManagement;
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

        #endregion Private members

        #region Properties

        public string GetMSBuildProjectExtensionsPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var msbuildProjectExtensionsPath = BuildProperties.GetPropertyValueWithDteFallback(ProjectBuildProperties.MSBuildProjectExtensionsPath);

            if (string.IsNullOrEmpty(msbuildProjectExtensionsPath))
            {
                return null;
            }

            return Path.Combine(ProjectDirectory, msbuildProjectExtensionsPath);
        }

        public IVsProjectBuildProperties BuildProperties { get; }

        public string CustomUniqueName => ProjectNames.CustomUniqueName;

        public string FullName => ProjectNames.FullName;

        public string ProjectDirectory { get; private set; }

        public string FullProjectPath { get; private set; }

        public async Task<bool> IsSupportedAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return VsHierarchyUtility.IsNuGetSupported(VsHierarchy);
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

                var packageVersion = BuildProperties.GetPropertyValueWithDteFallback(ProjectBuildProperties.PackageVersion);

                if (string.IsNullOrEmpty(packageVersion))
                {
                    packageVersion = BuildProperties.GetPropertyValueWithDteFallback(ProjectBuildProperties.Version);

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
            string projectDirectory,
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject,
            IVsProjectBuildProperties buildProperties,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsHierarchyItem);

            _vsHierarchyItem = vsHierarchyItem;
            _dteProject = new Lazy<EnvDTE.Project>(() => loadDteProject(_vsHierarchyItem.VsHierarchy));
            _threadingService = threadingService;
            FullProjectPath = fullProjectPath;
            ProjectNames = projectNames;
            BuildProperties = buildProperties;
            ProjectDirectory = projectDirectory;
        }

        public VsProjectAdapter(
            VsHierarchyItem vsHierarchyItem,
            ProjectNames projectNames,
            string fullProjectPath,
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject,
            IVsProjectBuildProperties buildProperties,
            IVsProjectThreadingService threadingService)
            : this(
                  vsHierarchyItem,
                  projectNames,
                  fullProjectPath,
                  Path.GetDirectoryName(fullProjectPath),
                  loadDteProject,
                  buildProperties,
                  threadingService)
        {
        }

        #endregion Constructors

        #region Getters

        public async Task<string[]> GetProjectTypeGuidsAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return VsHierarchyUtility.GetProjectTypeGuidsFromHierarchy(VsHierarchy);
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

        public async Task<NuGetFramework> GetTargetFrameworkAsync()
        {
            var frameworkString = await GetTargetFrameworkStringAsync();

            if (!string.IsNullOrEmpty(frameworkString))
            {
                return NuGetFramework.Parse(frameworkString);
            }

            return NuGetFramework.UnsupportedFramework;
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
            var platformIdentifier = BuildProperties.GetPropertyValueWithDteFallback(
                ProjectBuildProperties.TargetPlatformIdentifier);
            var platformVersion = BuildProperties.GetPropertyValueWithDteFallback(
                ProjectBuildProperties.TargetPlatformVersion);
            var platformMinVersion = BuildProperties.GetPropertyValueWithDteFallback(
                ProjectBuildProperties.TargetPlatformMinVersion);
            var targetFrameworkMoniker = BuildProperties.GetPropertyValueWithDteFallback(
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
