// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using NuGet.SolutionRestoreManager;
using SystemTasks = System.Threading.Tasks;

namespace NuGetVSExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
 //   [InstalledProductRegistration("#110", "#112", ProductVersion, IconResourceID = 400)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
 //   [ProvideMenuResource("Menus1.ctmenu", 1)]
    [Guid(PackageGuidString)]
    public sealed class RestoreManagerPackage : AsyncPackage
    {
        public const string ProductVersion = "3.6.0";

        /// <summary>
        /// NominateProjectPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "2b52ac92-4551-426d-bd34-c6d7d9fdd1c5";

        private IProjectSystemCache _projectSystemCache;

        private IVsSolutionRestoreService _solutionRestoreService;

        public RestoreManagerPackage()
        {

        }

        protected override async SystemTasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;

            _projectSystemCache = componentModel?.GetService<IProjectSystemCache>();
            Trace.WriteLineIf(_projectSystemCache != null, "Cache is found");

            _solutionRestoreService = componentModel?.GetService<IVsSolutionRestoreService>();
            Trace.WriteLineIf(_solutionRestoreService != null, "Restore Service is found");

            await base.InitializeAsync(cancellationToken, progress);
        }
    }
}
