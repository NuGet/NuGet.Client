// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
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
    [Name(nameof(MSBuildNuGetProjectProvider))]
    [Order(After = nameof(ProjectJsonProjectProvider))]
    internal class MSBuildNuGetProjectProvider : INuGetProjectProvider
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        public RuntimeTypeHandle ProjectType => typeof(VsMSBuildNuGetProject).TypeHandle;

        [ImportingConstructor]
        public MSBuildNuGetProjectProvider(IVsProjectThreadingService threadingService)
            : this(AsyncServiceProvider.GlobalProvider,
                   threadingService)
        { }

        public MSBuildNuGetProjectProvider(
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

            var projectSystem = await MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystemAsync(
                vsProjectAdapter,
                context.ProjectContext);

            await projectSystem.InitializeProperties();

            var projectServices = await CreateProjectServicesAsync(vsProjectAdapter, projectSystem);

            var folderNuGetProjectFullPath = context.PackagesPathFactory();

            // Project folder path is the packages config folder path
            var packagesConfigFolderPath = await vsProjectAdapter.GetProjectDirectoryAsync();

            return new VsMSBuildNuGetProject(
                vsProjectAdapter,
                projectSystem,
                folderNuGetProjectFullPath,
                packagesConfigFolderPath,
                projectServices);
        }

        private async Task<INuGetProjectServices> CreateProjectServicesAsync(
            IVsProjectAdapter vsProjectAdapter, VsMSBuildProjectSystem projectSystem)
        {
            var componentModel = await _componentModel.GetValueAsync();
            return new VsMSBuildProjectSystemServices(vsProjectAdapter, projectSystem, componentModel);
        }
    }
}
