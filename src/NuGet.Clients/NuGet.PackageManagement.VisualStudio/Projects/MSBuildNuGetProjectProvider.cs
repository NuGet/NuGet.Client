// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
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

        public bool TryCreateNuGetProject(
            IVsProjectAdapter vsProjectAdapter,
            ProjectProviderContext context,
            bool forceProjectType,
            out NuGetProject result)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(context);

            _threadingService.ThrowIfNotOnUIThread();

            result = null;

            var projectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(
                vsProjectAdapter,
                context.ProjectContext);

            var projectServices = CreateProjectServices(vsProjectAdapter, projectSystem);

            var folderNuGetProjectFullPath = context.PackagesPathFactory();

            // Project folder path is the packages config folder path
            var packagesConfigFolderPath = vsProjectAdapter.FullPath;

            result = new VsMSBuildNuGetProject(
                vsProjectAdapter,
                projectSystem,
                folderNuGetProjectFullPath,
                packagesConfigFolderPath,
                projectServices);

            return result != null;
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

        /// <summary>
        /// Implements project services in terms of <see cref="VsMSBuildProjectSystem"/>
        /// </summary>
        private class VsMSBuildProjectSystemServices
            : GlobalProjectServiceProvider
            , INuGetProjectServices
        {
            private readonly IVsProjectAdapter _vsProjectAdapter;
            private readonly VsMSBuildProjectSystem _vsProjectSystem;

            public IProjectBuildProperties BuildProperties => _vsProjectAdapter.BuildProperties;

            public IProjectSystemCapabilities Capabilities => _vsProjectSystem;

            public IProjectSystemReferencesReader ReferencesReader => _vsProjectSystem;

            public IProjectSystemReferencesService References => _vsProjectSystem;

            public IProjectSystemService ProjectSystem => _vsProjectSystem;

            public IProjectScriptHostService ScriptService { get; }

            public VsMSBuildProjectSystemServices(
                IVsProjectAdapter vsProjectAdapter,
                VsMSBuildProjectSystem vsProjectSystem,
                IComponentModel componentModel)
                : base(componentModel)
            {
                Assumes.Present(vsProjectAdapter);
                Assumes.Present(vsProjectSystem);

                _vsProjectAdapter = vsProjectAdapter;
                _vsProjectSystem = vsProjectSystem;

                ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
            }
        }
    }
}
