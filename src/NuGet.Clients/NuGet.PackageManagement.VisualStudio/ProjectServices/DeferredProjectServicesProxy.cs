// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Implementation of project services for a deferred project.
    /// It delegates read-only service requests to <see cref="WorkspaceProjectServices"/>.
    /// Other service requests that are not supported in DPL mode will be delegated to a fallback
    /// project services instance.
    /// </summary>
    internal class DeferredProjectServicesProxy : INuGetProjectServices
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IComponentModel _componentModel;
        private readonly IProjectSystemCapabilities _deferredProjectCapabilities;
        private readonly WorkspaceProjectServices _deferredProjectServices;
        private readonly Lazy<INuGetProjectServices> _fallbackProjectServices;
        private readonly IVsProjectThreadingService _threadingService;

        public DeferredProjectServicesProxy(
            IVsProjectAdapter vsProjectAdapter,
            IProjectSystemCapabilities deferredProjectCapabilities,
            Func<INuGetProjectServices> getFallbackProjectServices,
            IComponentModel componentModel)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(deferredProjectCapabilities);
            Assumes.Present(getFallbackProjectServices);
            Assumes.Present(componentModel);

            _vsProjectAdapter = vsProjectAdapter;
            _deferredProjectCapabilities = deferredProjectCapabilities;
            _componentModel = componentModel;

            _threadingService = _componentModel.GetService<IVsProjectThreadingService>();
            _fallbackProjectServices = new Lazy<INuGetProjectServices>(getFallbackProjectServices);
            _deferredProjectServices = new WorkspaceProjectServices(vsProjectAdapter, this);
        }

        public IProjectBuildProperties BuildProperties => _vsProjectAdapter.BuildProperties;

        public IProjectSystemCapabilities Capabilities
        {
            get
            {
                return _threadingService.ExecuteSynchronously(
                    async () =>
                    {
                        await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (_vsProjectAdapter.IsDeferred)
                        {
                            return _deferredProjectCapabilities;
                        }

                        return _fallbackProjectServices.Value.Capabilities;
                    });
            }
        }

        public IProjectSystemReferencesReader ReferencesReader
        {
            get
            {
                return _threadingService.ExecuteSynchronously(
                    async () =>
                    {
                        await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (_vsProjectAdapter.IsDeferred)
                        {
                            return _deferredProjectServices;
                        }

                        return _fallbackProjectServices.Value.ReferencesReader;
                    });
            }
        }

        public IProjectSystemService ProjectSystem => _fallbackProjectServices.Value.ProjectSystem;

        public IProjectSystemReferencesService References => _fallbackProjectServices.Value.References;

        public IProjectScriptHostService ScriptService => _fallbackProjectServices.Value.ScriptService;

        public T GetGlobalService<T>() where T : class => _componentModel.GetService<T>();
    }
}
