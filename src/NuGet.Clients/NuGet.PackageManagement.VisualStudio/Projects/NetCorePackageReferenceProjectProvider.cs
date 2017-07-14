// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Provides a method of creating <see cref="NetCorePackageReferenceProject"/> instance.
    /// </summary>
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(NetCorePackageReferenceProjectProvider))]
    [Microsoft.VisualStudio.Utilities.Order(After = nameof(ProjectKNuGetProjectProvider))]
    public class NetCorePackageReferenceProjectProvider : INuGetProjectProvider
    {
        private static readonly string PackageReference = ProjectStyle.PackageReference.ToString();

        private readonly IProjectSystemCache _projectSystemCache;

        private readonly AsyncLazy<IComponentModel> _componentModel;

        private readonly IVsProjectThreadingService _threadingService;

        // Reason it's lazy<object> is because we don't want to load any CPS assemblies untill
        // we're really going to use any of CPS api. Which is why we also don't use nameof or typeof apis.
        [Import("Microsoft.VisualStudio.ProjectSystem.IProjectServiceAccessor")]
        private Lazy<object> ProjectServiceAccessor { get; set; }

        public RuntimeTypeHandle ProjectType => typeof(NetCorePackageReferenceProject).TypeHandle;

        [ImportingConstructor]
        public NetCorePackageReferenceProjectProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider vsServiceProvider,
            IProjectSystemCache projectSystemCache,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsServiceProvider);
            Assumes.Present(projectSystemCache);
            Assumes.Present(threadingService);

            _projectSystemCache = projectSystemCache;
            _threadingService = threadingService;

            _componentModel = new AsyncLazy<IComponentModel>(
                async () =>
                {
                    await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return vsServiceProvider.GetService<SComponentModel, IComponentModel>();
                },
                _threadingService.JoinableTaskFactory);
        }

        public bool TryCreateNuGetProject(
            IVsProjectAdapter vsProject, 
            ProjectProviderContext context, 
            bool forceProjectType,
            out NuGetProject result)
        {
            Assumes.Present(vsProject);
            Assumes.Present(context);

            result = null;

            // The project must be an IVsHierarchy.
            var hierarchy = vsProject.VsHierarchy;
            
            if (hierarchy == null)
            {
                return false;
            }

            // Check if the project is not CPS capable or if it is CPS capable then it does not have TargetFramework(s), if so then return false
            var isCapabilityMatchCPS = _threadingService.ExecuteSynchronously(
                async () =>
                {
                    await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return hierarchy.IsCapabilityMatch("CPS");
                });

            if (!isCapabilityMatchCPS)
            {
                return false;
            }

            var buildProperties = vsProject.BuildProperties;

            // read MSBuild property RestoreProjectStyle, TargetFramework, and TargetFrameworks
            var restoreProjectStyle = buildProperties.GetPropertyValue(ProjectBuildProperties.RestoreProjectStyle);
            var targetFramework = buildProperties.GetPropertyValue(ProjectBuildProperties.TargetFramework);
            var targetFrameworks = buildProperties.GetPropertyValue(ProjectBuildProperties.TargetFrameworks);

            // check for RestoreProjectStyle property is set and if not set to PackageReference then return false
            if (!(string.IsNullOrEmpty(restoreProjectStyle) ||
                restoreProjectStyle.Equals(PackageReference, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            // check whether TargetFramework or TargetFrameworks property is set, else return false
            else if (string.IsNullOrEmpty(targetFramework) && string.IsNullOrEmpty(targetFrameworks))
            {
                return false;
            }

            // Lazy load the CPS enabled JoinableTaskFactory for the UI.
            NuGetUIThreadHelper.SetJoinableTaskFactoryFromService(ProjectServiceAccessor.Value as IProjectServiceAccessor);

            var projectNames = vsProject.ProjectNames;
            var fullProjectPath = vsProject.FullProjectPath;
            var unconfiguredProject = _threadingService.ExecuteSynchronously(() => GetUnconfiguredProjectAsync(vsProject.Project));

            var projectServices = _threadingService.ExecuteSynchronously(() => CreateProjectServicesAsync(vsProject));

            result = new NetCorePackageReferenceProject(
                vsProject.ProjectName,
                vsProject.CustomUniqueName,
                fullProjectPath,
                _projectSystemCache,
                vsProject,
                unconfiguredProject,
                projectServices,
                vsProject.ProjectId);

            return true;
        }

        private async Task<INuGetProjectServices> CreateProjectServicesAsync(IVsProjectAdapter vsProjectAdapter)
        {
            var componentModel = await _componentModel.GetValueAsync();

            return new NetCoreProjectSystemServices(vsProjectAdapter, componentModel);
        }

        private async Task<UnconfiguredProject> GetUnconfiguredProjectAsync(EnvDTE.Project project)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var context = project as IVsBrowseObjectContext;
            if (context == null)
            {
                // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }
            return context?.UnconfiguredProject;
        }
    }
}
