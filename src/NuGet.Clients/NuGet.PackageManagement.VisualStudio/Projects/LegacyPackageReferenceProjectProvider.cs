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
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;


namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(LegacyPackageReferenceProjectProvider))]
    [Order(After = nameof(CpsPackageReferenceProjectProvider))]
    public sealed class LegacyPackageReferenceProjectProvider : INuGetProjectProvider
    {
        private static readonly string PackageReference = ProjectStyle.PackageReference.ToString();

        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        public RuntimeTypeHandle ProjectType => typeof(LegacyPackageReferenceProject).TypeHandle;

        [ImportingConstructor]
        public LegacyPackageReferenceProjectProvider(
            IVsProjectThreadingService threadingService)
            : this(AsyncServiceProvider.GlobalProvider,
                   threadingService)
        { }

        public LegacyPackageReferenceProjectProvider(
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

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

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

            var asVSProject4 = vsProjectAdapter.Project.Object as VSProject4;

            // A legacy CSProj must cast to VSProject4 to manipulate package references
            if (asVSProject4 == null)
            {
                return null;
            }

            // Check for RestoreProjectStyle property
            var restoreProjectStyle = await vsProjectAdapter.BuildProperties.GetPropertyValueAsync(
                ProjectBuildProperties.RestoreProjectStyle);

            // For legacy csproj, either the RestoreProjectStyle must be set to PackageReference or
            // project has atleast one package dependency defined as PackageReference
            if (forceCreate
                || PackageReference.Equals(restoreProjectStyle, StringComparison.OrdinalIgnoreCase)
                || (asVSProject4.PackageReferences?.InstalledPackages?.Length ?? 0) > 0)
            {
                var nominatesOnSolutionLoad = await vsProjectAdapter.IsCapabilityMatchAsync(NuGet.VisualStudio.IDE.ProjectCapabilities.PackageReferences);
                return CreateCoreProjectSystemServices(vsProjectAdapter, componentModel, nominatesOnSolutionLoad);
            }

            return null;
        }

        private INuGetProjectServices CreateCoreProjectSystemServices(
                IVsProjectAdapter vsProjectAdapter, IComponentModel componentModel, bool nominatesOnSolutionLoad)
        {
            return new VsManagedLanguagesProjectSystemServices(vsProjectAdapter, componentModel, nominatesOnSolutionLoad);
        }
    }
}
