// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IVsProjectAdapterProvider))]
    internal class VsProjectAdapterProvider : IVsProjectAdapterProvider
    {
        private readonly Lazy<IDeferredProjectWorkspaceService> _workspaceService = null;
        private readonly IVsProjectThreadingService _threadingService;

        private readonly Lazy<IVsSolution> _vsSolution;

        [ImportingConstructor]
        public VsProjectAdapterProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
#if !VS14
            Lazy<IDeferredProjectWorkspaceService> workspaceService,
#endif
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(serviceProvider);
            Assumes.Present(threadingService);
#if !VS14
            Assumes.Present(workspaceService);

            _workspaceService = workspaceService;
#endif
            _threadingService = threadingService;

            _vsSolution = new Lazy<IVsSolution>(() => serviceProvider.GetService<SVsSolution, IVsSolution>());
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

            _threadingService.ThrowIfNotOnUIThread();

            var vsHierarchyItem = VsHierarchyItem.FromDteProject(dteProject);
            Func<IVsHierarchy, EnvDTE.Project> loadDteProject = _ => dteProject;

            var buildStorageProperty = vsHierarchyItem.VsHierarchy as IVsBuildPropertyStorage;
            var vsBuildProperties = new VsProjectBuildProperties(
                dteProject, buildStorageProperty, _threadingService);

            var projectNames = await ProjectNames.FromDTEProjectAsync(dteProject);
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

        public async Task<IVsProjectAdapter> CreateAdapterForDeferredProjectAsync(IVsHierarchy project)
        {
            Assumes.Present(project);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsHierarchyItem = VsHierarchyItem.FromVsHierarchy(project);
            var fullProjectPath = VsHierarchyUtility.GetProjectPath(project);

            var uniqueName = string.Empty;
            _vsSolution.Value.GetUniqueNameOfProject(project, out uniqueName);

            var projectNames = new ProjectNames(
                fullName: fullProjectPath,
                uniqueName: uniqueName,
                shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                customUniqueName: GetCustomUniqueName(uniqueName));

            var workspaceBuildProperties = new WorkspaceProjectBuildProperties(
                fullProjectPath, _workspaceService.Value, _threadingService);

            var projectTypeGuid = await _workspaceService.Value.GetProjectTypeGuidAsync(fullProjectPath);

            return new VsProjectAdapter(
                vsHierarchyItem,
                projectNames,
                fullProjectPath,
                projectTypeGuid,
                EnsureProjectIsLoaded,
                workspaceBuildProperties,
                _threadingService,
                _workspaceService.Value);
        }

        public EnvDTE.Project EnsureProjectIsLoaded(IVsHierarchy project)
        {
            return _threadingService.ExecuteSynchronously(async () =>
            {
                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // 1. Ask the solution to load the required project. To reduce wait time,
                //    we load only the project we need, not the entire solution.
                ErrorHandler.ThrowOnFailure(project.GetGuidProperty(
                    (uint)VSConstants.VSITEMID.Root,
                    (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                    out Guid projectGuid));

                var asVsSolution4 = _vsSolution.Value as IVsSolution4;
                Assumes.Present(asVsSolution4);

                ErrorHandler.ThrowOnFailure(asVsSolution4.EnsureProjectIsLoaded(
                    projectGuid,
                    (uint)__VSBSLFLAGS.VSBSLFLAGS_None));

                // 2. After the project is loaded, grab the latest IVsHierarchy object.
                ErrorHandler.ThrowOnFailure(_vsSolution.Value.GetProjectOfGuid(
                    projectGuid,
                    out IVsHierarchy loadedProject));
                Assumes.Present(loadedProject);

                object extObject = null;
                ErrorHandler.ThrowOnFailure(loadedProject.GetProperty(
                    (uint)VSConstants.VSITEMID.Root,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out extObject));

                var dteProject = extObject as EnvDTE.Project;
                Assumes.Present(dteProject);

                return dteProject;
            });
        }

        // Get DTE-like customUniqueName from unique Name
        // eg: A/A.proj -> A, foo/A/A.csproj -> foo/A
        private string GetCustomUniqueName(string uniqueName)
        {
            var names = uniqueName.Split(Path.DirectorySeparatorChar);
            var nameParts = new List<string>(names);

            if (nameParts.Count == 1)
            {
                return nameParts[0];
            }
            else
            {
                var fileName = nameParts[nameParts.Count - 1];
                var directoryName = nameParts[nameParts.Count - 2];
                nameParts.RemoveAt(nameParts.Count - 1);
                nameParts.RemoveAt(nameParts.Count - 1);

                nameParts.Add(Path.GetFileNameWithoutExtension(fileName));

                return string.Join(Path.DirectorySeparatorChar.ToString(), nameParts);
            }
        }
    }
}
