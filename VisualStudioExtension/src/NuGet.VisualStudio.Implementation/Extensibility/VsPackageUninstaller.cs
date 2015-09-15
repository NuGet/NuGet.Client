// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageUninstaller))]
    public class VsPackageUninstaller : IVsPackageUninstaller
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private Configuration.ISettings _settings;
        private ISolutionManager _solutionManager;
        private IDeleteOnRestartManager _deleteOnRestartManager;

        private JoinableTaskFactory PumpingJTF { get; }

        [ImportingConstructor]
        public VsPackageUninstaller(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager,
            IDeleteOnRestartManager deleteOnRestartManager)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;

            PumpingJTF = new PumpingJTF(ThreadHelper.JoinableTaskContext);
            _deleteOnRestartManager = deleteOnRestartManager;
        }

        public void UninstallPackage(Project project, string packageId, bool removeDependencies)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId"));
            }

            PumpingJTF.Run(async delegate
                {
                    NuGetPackageManager packageManager =
                       new NuGetPackageManager(
                           _sourceRepositoryProvider,
                           _settings,
                           _solutionManager,
                           _deleteOnRestartManager);

                    UninstallationContext uninstallContext = new UninstallationContext(removeDependencies, false);
                    VSAPIProjectContext projectContext = new VSAPIProjectContext();

                    // find the project
                    NuGetProject nuGetProject = await PackageManagementHelpers.GetProjectAsync(_solutionManager, project, projectContext);

                    // uninstall the package
                    await packageManager.UninstallPackageAsync(nuGetProject, packageId, uninstallContext, projectContext, CancellationToken.None);
                });
        }
    }
}
