// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Implementation of read-only project services available in DPL mode.
    /// </summary>
    internal class WorkspaceProjectServices
        : IProjectSystemReferencesReader
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IDeferredProjectWorkspaceService _workspaceService;
        private readonly IVsProjectThreadingService _threadingService;

        private readonly AsyncLazy<IMSBuildProjectDataService> _buildProjectDataService;
        private readonly string _fullProjectPath;

        private IMSBuildProjectDataService BuildProjectDataService => _threadingService.ExecuteSynchronously(_buildProjectDataService.GetValueAsync);

        public WorkspaceProjectServices(
            IVsProjectAdapter vsProjectAdapter,
            INuGetProjectServices projectServices)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(projectServices);

            _vsProjectAdapter = vsProjectAdapter;
            _fullProjectPath = vsProjectAdapter.FullProjectPath;

            _workspaceService = projectServices.GetGlobalService<IDeferredProjectWorkspaceService>();
            Assumes.Present(_workspaceService);

            _threadingService = projectServices.GetGlobalService<IVsProjectThreadingService>();
            Assumes.Present(_threadingService);

            _buildProjectDataService = new AsyncLazy<IMSBuildProjectDataService>(
                () => _workspaceService.GetMSBuildProjectDataServiceAsync(_fullProjectPath),
                _threadingService.JoinableTaskFactory);
        }

        public async Task<IEnumerable<LibraryDependency>> GetPackageReferencesAsync(
            NuGetFramework targetFramework,
            CancellationToken token)
        {
            var dataService = await _buildProjectDataService.GetValueAsync(token);

            var referenceItems = await dataService.GetProjectItems(ProjectItems.PackageReference, token);

            var packageReferences = referenceItems
                .Select(ToPackageLibraryDependency)
                .ToList();

            return packageReferences;
        }

        public async Task<IEnumerable<ProjectRestoreReference>> GetProjectReferencesAsync(
            Common.ILogger logger,
            CancellationToken token)
        {
            var references = await _workspaceService.GetProjectReferencesAsync(_fullProjectPath);

            return references
                .Select(reference => new ProjectRestoreReference
                {
                    ProjectPath = reference,
                    ProjectUniqueName = reference
                })
                .ToList();
        }

        private static LibraryDependency ToPackageLibraryDependency(MSBuildProjectItemData item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.EvaluatedInclude,
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                GetItemMetadataValueOrDefault(item, ProjectItemProperties.IncludeAssets),
                GetItemMetadataValueOrDefault(item, ProjectItemProperties.ExcludeAssets),
                GetItemMetadataValueOrDefault(item, ProjectItemProperties.PrivateAssets));

            return dependency;
        }

        private static VersionRange GetVersionRange(MSBuildProjectItemData item)
        {
            var versionRange = GetItemMetadataValueOrDefault(item, ProjectBuildProperties.Version);

            if (!string.IsNullOrEmpty(versionRange))
            {
                return VersionRange.Parse(versionRange);
            }

            return VersionRange.All;
        }

        private static string GetItemMetadataValueOrDefault(
            MSBuildProjectItemData item, string propertyName, string defaultValue = "")
        {
            if (item.Metadata.TryGetValue(propertyName, out var propertyValue))
            {
                return propertyValue;
            }

            return defaultValue;
        }
    }
}
