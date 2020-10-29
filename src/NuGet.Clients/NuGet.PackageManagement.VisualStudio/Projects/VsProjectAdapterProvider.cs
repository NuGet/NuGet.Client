// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IVsProjectAdapterProvider))]
    internal class VsProjectAdapterProvider : IVsProjectAdapterProvider
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IDeferredProjectWorkspaceService> _workspaceService;
        private readonly AsyncLazy<IVsSolution5> _vsSolution5;

        [ImportingConstructor]
        public VsProjectAdapterProvider(
            [Import(typeof(SAsyncServiceProvider))]
            IAsyncServiceProvider serviceProvider,
            IVsProjectThreadingService threadingService)
            : this(
                  threadingService,
                  new AsyncLazy<IDeferredProjectWorkspaceService>(() => serviceProvider.GetServiceAsync<IDeferredProjectWorkspaceService>(), threadingService.JoinableTaskFactory),
                  new AsyncLazy<IVsSolution5>(() => serviceProvider.GetServiceAsync<SVsSolution, IVsSolution5>(), threadingService.JoinableTaskFactory))
        {
        }

        internal VsProjectAdapterProvider(
            IVsProjectThreadingService threadingService,
            AsyncLazy<IDeferredProjectWorkspaceService> workspaceService,
            AsyncLazy<IVsSolution5> vsSolution5)
        {
            Assumes.Present(threadingService);
            Assumes.Present(workspaceService);
            Assumes.Present(vsSolution5);

            _threadingService = threadingService;
            _workspaceService = workspaceService;
            _vsSolution5 = vsSolution5;
        }

        public async Task<bool> EntityExistsAsync(string filePath)
        {
            var workspaceService = await _workspaceService.GetValueAsync();
            return await workspaceService.EntityExistsAsync(filePath);
        }

        public IVsProjectAdapter CreateAdapterForFullyLoadedProject(EnvDTE.Project dteProject)
        {
            return _threadingService.ExecuteSynchronously(
                () => CreateAdapterForFullyLoadedProjectAsync(dteProject));
        }

        public async Task<IVsProjectAdapter> CreateAdapterForFullyLoadedProjectAsync(EnvDTE.Project dteProject)
        {
            Assumes.Present(dteProject);

            // Get services while we might be on background thread
            var vsSolution5 = await _vsSolution5.GetValueAsync();

            // switch to main thread and use services we know must be done on main thread.
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsHierarchyItem = await VsHierarchyItem.FromDteProjectAsync(dteProject);
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject = _ => dteProject;

            var buildStorageProperty = vsHierarchyItem.VsHierarchy as IVsBuildPropertyStorage;
            var vsBuildProperties = new VsProjectBuildProperties(
                dteProject, buildStorageProperty, _threadingService);

            var projectNames = await ProjectNames.FromDTEProjectAsync(dteProject, vsSolution5);
            var fullProjectPath = dteProject.GetFullProjectPath();

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
