// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;


namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(ProjectJsonProjectProvider))]
    [Order(After = nameof(LegacyPackageReferenceProjectProvider))]
    internal class ProjectJsonProjectProvider : INuGetProjectProvider
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        public RuntimeTypeHandle ProjectType => typeof(VsProjectJsonNuGetProject).TypeHandle;

        [ImportingConstructor]
        public ProjectJsonProjectProvider(IVsProjectThreadingService threadingService)
            : this(AsyncServiceProvider.GlobalProvider, threadingService)
        { }

        public ProjectJsonProjectProvider(
            IAsyncServiceProvider vsServiceProvider,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsServiceProvider);
            Assumes.Present(threadingService);

            _threadingService = threadingService;

            _componentModel = new AsyncLazy<IComponentModel>(
                async () =>
                {
                    return await vsServiceProvider.GetServiceAsync<SComponentModel, IComponentModel>();
                },
                _threadingService.JoinableTaskFactory);
        }

        public async Task<NuGetProject> TryCreateNuGetProjectAsync(
            IVsProjectAdapter vsProjectAdapter,
            ProjectProviderContext context,
            bool forceProjectType)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(context);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var guids = await vsProjectAdapter.GetProjectTypeGuidsAsync();

            // Web sites cannot have project.json
            if (guids.Contains(VsProjectTypes.WebSiteProjectTypeGuid, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            // Find the project file path
            var projectFilePath = vsProjectAdapter.FullProjectPath;

            if (!string.IsNullOrEmpty(projectFilePath))
            {
                var msbuildProjectFile = new FileInfo(projectFilePath);
                var projectNameFromMSBuildPath = Path.GetFileNameWithoutExtension(msbuildProjectFile.Name);

                // Treat projects with project.json as build integrated projects
                // Search for projectName.project.json first, then project.json
                // If the name cannot be determined, search only for project.json
                string projectJsonPath = null;
                if (string.IsNullOrEmpty(projectNameFromMSBuildPath))
                {
                    projectJsonPath = Path.Combine(msbuildProjectFile.DirectoryName,
                        Common.ProjectJsonPathUtilities.ProjectConfigFileName);
                }
                else
                {
                    projectJsonPath = Common.ProjectJsonPathUtilities.GetProjectConfigPath(
                        msbuildProjectFile.DirectoryName,
                        projectNameFromMSBuildPath);
                }

                if (File.Exists(projectJsonPath))
                {
                    var projectServices = await CreateProjectServicesAsync(vsProjectAdapter);

                    return new VsProjectJsonNuGetProject(
                        projectJsonPath,
                        msbuildProjectFile.FullName,
                        vsProjectAdapter,
                        projectServices);
                }
            }

            return null;
        }

        private async Task<INuGetProjectServices> CreateProjectServicesAsync(IVsProjectAdapter vsProjectAdapter)
        {
            var componentModel = await _componentModel.GetValueAsync();
            return new VsCoreProjectSystemServices(vsProjectAdapter, componentModel);
        }
    }
}
