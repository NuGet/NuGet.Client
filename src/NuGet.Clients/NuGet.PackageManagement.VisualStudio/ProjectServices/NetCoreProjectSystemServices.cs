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
    /// Represent net core project systems in visual studio.
    /// </summary>
    internal class NetCoreProjectSystemServices :
        GlobalProjectServiceProvider,
        INuGetProjectServices
    {
        public NetCoreProjectSystemServices(
            IVsProjectAdapter vsProjectAdapter,
            IComponentModel componentModel) 
            : base(componentModel)
        {
            Assumes.Present(vsProjectAdapter);

            ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
        }

        public IProjectBuildProperties BuildProperties => throw new NotSupportedException();

        public IProjectSystemCapabilities Capabilities => throw new NotSupportedException();

        public IProjectSystemReferencesReader ReferencesReader => throw new NotSupportedException();

        public IProjectSystemReferencesService References => throw new NotSupportedException();

        public IProjectSystemService ProjectSystem => throw new NotSupportedException();

        public IProjectScriptHostService ScriptService { get; }

    }
}
