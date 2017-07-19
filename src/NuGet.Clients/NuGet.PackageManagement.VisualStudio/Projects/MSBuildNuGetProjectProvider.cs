// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(MSBuildNuGetProjectProvider))]
    [Order(After = nameof(ProjectJsonProjectProvider))]
    internal class MSBuildNuGetProjectProvider : INuGetProjectProvider
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly Lazy<IComponentModel> _componentModel;

        public RuntimeTypeHandle ProjectType => typeof(VsMSBuildNuGetProject).TypeHandle;

        [ImportingConstructor]
        public MSBuildNuGetProjectProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider vsServiceProvider,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsServiceProvider);
            Assumes.Present(threadingService);

            _threadingService = threadingService;

            _componentModel = new Lazy<IComponentModel>(
                () => vsServiceProvider.GetService<SComponentModel, IComponentModel>());
        }

        public async Task<NuGetProject> TryCreateNuGetProjectAsync(
            IVsProjectAdapter vsProjectAdapter,
            ProjectProviderContext context,
            bool forceProjectType)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(context);

            _threadingService.ThrowIfNotOnUIThread();

            var projectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(
            vsProjectAdapter,
            context.ProjectContext);

            var projectServices = CreateProjectServices(vsProjectAdapter, projectSystem);

            var folderNuGetProjectFullPath = context.PackagesPathFactory();

            // Project folder path is the packages config folder path
            var packagesConfigFolderPath = vsProjectAdapter.ProjectDirectory;

            return await System.Threading.Tasks.Task.FromResult(new VsMSBuildNuGetProject(
                vsProjectAdapter,
                projectSystem,
                folderNuGetProjectFullPath,
                packagesConfigFolderPath,
                projectServices));
        }

        private INuGetProjectServices CreateProjectServices(
            IVsProjectAdapter vsProjectAdapter, VsMSBuildProjectSystem projectSystem)
        {
            var componentModel = _componentModel.Value;

            if (vsProjectAdapter.IsDeferred)
            {
                return new DeferredProjectServicesProxy(
                    vsProjectAdapter,
                    new DeferredProjectCapabilities { SupportsPackageReferences = false },
                    () => new VsMSBuildProjectSystemServices(vsProjectAdapter, projectSystem, componentModel),
                    componentModel);
            }
            else
            {
                return new VsMSBuildProjectSystemServices(vsProjectAdapter, projectSystem, componentModel);
            }
        }
    }
}
