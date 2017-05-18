// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class VsProjectSystemServices : INuGetProjectServices
    {
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        private IVsProjectAdapter ProjectAdapter { get; set; }

        public IProjectBuildProperties BuildProperties
        {
            get
            {
                Assumes.Present(ProjectAdapter);
                return ProjectAdapter.BuildProperties;
            }
        }

        public IProjectSystemCapabilities Capabilities { get; set; }

        public IProjectSystemReferencesReader ReferencesReader { get; set; }

        public IProjectSystemService ProjectSystem { get; set; }

        public IProjectSystemReferencesService References { get; set; }

        public IProjectScriptHostService ScriptService { get; set; }

        [ImportingConstructor]
        public VsProjectSystemServices(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider vsServiceProvider,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsServiceProvider);
            Assumes.Present(threadingService);

            _threadingService = threadingService;

            _componentModel = new AsyncLazy<IComponentModel>(
                async () =>
                {
                    await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return vsServiceProvider.GetService<SComponentModel, IComponentModel>();
                },
                _threadingService.JoinableTaskFactory);
        }

        public void AttachProjectAdapter(IVsProjectAdapter vsProjectAdapter)
        {
            Assumes.Present(vsProjectAdapter);

            ProjectAdapter = vsProjectAdapter;
            ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
        }

        public T GetGlobalService<T>() where T : class
        {
            return _threadingService.ExecuteSynchronously(
                async () => {
                    var componentModel = await _componentModel.GetValueAsync();
                    return componentModel.GetService<T>();
                });
        }
    }
}
