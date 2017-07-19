// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.VisualStudio;
using VSLangProj150;
using ProjectSystem = Microsoft.VisualStudio.ProjectSystem;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(LegacyPackageReferenceProjectProvider))]
    [Order(After = nameof(NetCorePackageReferenceProjectProvider))]
    public sealed class LegacyPackageReferenceProjectProvider : INuGetProjectProvider
    {
        private static readonly string PackageReference = ProjectStyle.PackageReference.ToString();

        private readonly Lazy<IDeferredProjectWorkspaceService> _workspaceService;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        // Reason it's lazy<object> is because we don't want to load any CPS assemblies untill
        // we're really going to use any of CPS api. Which is why we also don't use nameof or typeof apis.
        [Import("Microsoft.VisualStudio.ProjectSystem.IProjectServiceAccessor")]
        private Lazy<object> ProjectServiceAccessor { get; set; }

        public RuntimeTypeHandle ProjectType => typeof(LegacyPackageReferenceProject).TypeHandle;

        [ImportingConstructor]
        public LegacyPackageReferenceProjectProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider vsServiceProvider,
            Lazy<IDeferredProjectWorkspaceService> workspaceService,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsServiceProvider);
            Assumes.Present(workspaceService);
            Assumes.Present(threadingService);

            _workspaceService = workspaceService;
            _threadingService = threadingService;

            _componentModel = new AsyncLazy<IComponentModel>(
                async () =>
                {
                    await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return vsServiceProvider.GetService<SComponentModel, IComponentModel>();
                },
                _threadingService.JoinableTaskFactory);
        }

        public async Task<NuGetProject> TryCreateNuGetProjectAsync(
            IVsProjectAdapter vsProjectAdapter,
            ProjectProviderContext _,
            bool forceProjectType)
        {
            Assumes.Present(vsProjectAdapter);

            _threadingService.ThrowIfNotOnUIThread();

            var projectServices = await TryCreateProjectServicesAsync(
                    vsProjectAdapter,
                    forceCreate: forceProjectType);

            if (projectServices == null)
            {
                return null;
            }

            return new LegacyPackageReferenceProject(
                vsProjectAdapter,
                vsProjectAdapter.ProjectId,
                projectServices,
                _threadingService);
        }

        /// <summary>
        /// Is this project a non-CPS package reference based csproj?
        /// </summary>
        private async Task<INuGetProjectServices> TryCreateProjectServicesAsync(
            IVsProjectAdapter vsProjectAdapter, bool forceCreate)
        {
            var componentModel = await _componentModel.GetValueAsync();

            // Check for RestoreProjectStyle property
            var restoreProjectStyle = await vsProjectAdapter.BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.RestoreProjectStyle);

            if (vsProjectAdapter.IsDeferred)
            {
                if (!forceCreate &&
                    !PackageReference.Equals(restoreProjectStyle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!await ProjectHasPackageReferencesAsync(vsProjectAdapter))
                    {
                        return null;
                    }
                }

                return new DeferredProjectServicesProxy(
                    vsProjectAdapter,
                    new DeferredProjectCapabilities { SupportsPackageReferences = true },
                    () => CreateCoreProjectSystemServices(vsProjectAdapter, componentModel),
                    componentModel);
            }
            else
            {
                var asVSProject4 = vsProjectAdapter.Project.Object as VSProject4;

                // A legacy CSProj must cast to VSProject4 to manipulate package references
                if (asVSProject4 == null)
                {
                    return null;
                }

                // For legacy csproj, either the RestoreProjectStyle must be set to PackageReference or
                // project has atleast one package dependency defined as PackageReference
                if (forceCreate
                    || PackageReference.Equals(restoreProjectStyle, StringComparison.OrdinalIgnoreCase)
                    || (asVSProject4.PackageReferences?.InstalledPackages?.Length ?? 0) > 0)
                {
                    return CreateCoreProjectSystemServices(vsProjectAdapter, componentModel);
                }
            }

            return null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private async Task<bool> ProjectHasPackageReferencesAsync(IVsProjectAdapter vsProjectAdapter)
        {
            var buildProjectDataService = await _workspaceService.Value.GetMSBuildProjectDataServiceAsync(
                vsProjectAdapter.FullProjectPath);
            Assumes.Present(buildProjectDataService);

            var referenceItems = await buildProjectDataService.GetProjectItems(
                ProjectItems.PackageReference, CancellationToken.None);
            if (referenceItems == null || referenceItems.Count == 0)
            {
                return false;
            }

            return true;
        }

        private INuGetProjectServices CreateCoreProjectSystemServices(
                IVsProjectAdapter vsProjectAdapter, IComponentModel componentModel)
        {
            // Lazy load the CPS enabled JoinableTaskFactory for the UI.
            NuGetUIThreadHelper.SetJoinableTaskFactoryFromService(ProjectServiceAccessor.Value as ProjectSystem.IProjectServiceAccessor);

            return new VsManagedLanguagesProjectSystemServices(vsProjectAdapter, componentModel);
        }
    }
}
