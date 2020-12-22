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
    /// Represents Visual Studio core project system in the integrated development environment (IDE).
    /// </summary>
    internal class VsCoreProjectSystemServices
        : GlobalProjectServiceProvider
        , INuGetProjectServices
        , IProjectSystemCapabilities
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;

        public bool SupportsPackageReferences => false;

        public bool NominatesOnSolutionLoad => false;

        #region INuGetProjectServices

        public IProjectBuildProperties BuildProperties => _vsProjectAdapter.BuildProperties;

        public IProjectSystemCapabilities Capabilities => this;

        public IProjectSystemReferencesReader ReferencesReader { get; }

        public IProjectSystemReferencesService References => throw new NotSupportedException();

        public IProjectSystemService ProjectSystem { get; }

        public IProjectScriptHostService ScriptService { get; }

        #endregion INuGetProjectServices

        public VsCoreProjectSystemServices(
            IVsProjectAdapter vsProjectAdapter,
            IComponentModel componentModel)
            : base(componentModel)
        {
            Assumes.Present(vsProjectAdapter);

            _vsProjectAdapter = vsProjectAdapter;

            _threadingService = GetGlobalService<IVsProjectThreadingService>();
            Assumes.Present(_threadingService);
            ProjectSystem = new VsCoreProjectSystem(_vsProjectAdapter);

            ReferencesReader = new VsCoreProjectSystemReferenceReader(vsProjectAdapter, this);
            ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
        }
    }
}
