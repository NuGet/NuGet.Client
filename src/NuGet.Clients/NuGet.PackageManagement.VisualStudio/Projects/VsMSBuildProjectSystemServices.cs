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
    /// Implements project services in terms of <see cref="VsMSBuildProjectSystem"/>
    /// </summary>
    internal class VsMSBuildProjectSystemServices
        : GlobalProjectServiceProvider
        , INuGetProjectServices
        , IProjectSystemCapabilities
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly VsMSBuildProjectSystem _vsProjectSystem;

        #region INuGetProjectServices

        public IProjectBuildProperties BuildProperties => _vsProjectAdapter.BuildProperties;

        public IProjectSystemCapabilities Capabilities => this;

        public IProjectSystemReferencesReader ReferencesReader { get; }

        public IProjectSystemReferencesService References => throw new NotSupportedException();

        public IProjectSystemService ProjectSystem => _vsProjectSystem;

        public IProjectScriptHostService ScriptService { get; }

        #endregion INuGetProjectServices

        public bool SupportsPackageReferences
        {
            get
            {
                return _threadingService.ExecuteSynchronously(async () =>
                {
                    await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return _vsProjectAdapter.Project.Object is VSLangProj150.VSProject4;
                });
            }
        }

        public bool NominatesOnSolutionLoad => false;

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

            _threadingService = GetGlobalService<IVsProjectThreadingService>();
            Assumes.Present(_threadingService);

            ReferencesReader = new VsCoreProjectSystemReferenceReader(vsProjectAdapter, this);
            ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
        }
    }
}
