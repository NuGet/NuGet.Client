// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IVsProjectAdapterProvider))]
    internal class VsProjectAdapterProvider : IVsProjectAdapterProvider
    {
        private readonly Lazy<IDeferredProjectWorkspaceService> _workspaceService = null;
        private readonly IVsProjectThreadingService _threadingService;

        private readonly Lazy<IVsSolution5> _vsSolution5;

        [ImportingConstructor]
        public VsProjectAdapterProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            Lazy<IDeferredProjectWorkspaceService> workspaceService,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(serviceProvider);
            Assumes.Present(threadingService);
            Assumes.Present(workspaceService);

            _workspaceService = workspaceService;
            _threadingService = threadingService;
            _vsSolution5 = new Lazy<IVsSolution5>(() => serviceProvider.GetService<SVsSolution, IVsSolution5>());
        }

        public async Task<bool> EntityExistsAsync(string filePath)
        {
            return await _workspaceService.Value.EntityExistsAsync(filePath);
        }

        public IVsProjectAdapter CreateAdapterForFullyLoadedProject(EnvDTE.Project dteProject)
        {
            return _threadingService.ExecuteSynchronously(
                () => CreateAdapterForFullyLoadedProjectAsync(dteProject));
        }

        public async Task<IVsProjectAdapter> CreateAdapterForFullyLoadedProjectAsync(EnvDTE.Project dteProject)
        {
            Assumes.Present(dteProject);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsHierarchyItem = VsHierarchyItem.FromDteProject(dteProject);
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject = _ => dteProject;

            var buildStorageProperty = vsHierarchyItem.VsHierarchy as IVsBuildPropertyStorage;
            var vsBuildProperties = new VsProjectBuildProperties(
                dteProject, buildStorageProperty, _threadingService);

            var projectNames = await ProjectNames.FromDTEProjectAsync(dteProject, _vsSolution5.Value);
            var fullProjectPath = EnvDTEProjectInfoUtility.GetFullProjectPath(dteProject);

            return new VsProjectAdapter(
                vsHierarchyItem,
                projectNames,
                fullProjectPath,
                dteProject.Kind,
                loadDteProject,
                vsBuildProperties,
                _threadingService);
        }
    }
}
