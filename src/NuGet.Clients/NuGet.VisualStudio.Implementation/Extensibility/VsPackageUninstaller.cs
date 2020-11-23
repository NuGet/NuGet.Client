// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageUninstaller))]
    public class VsPackageUninstaller : IVsPackageUninstaller
    {
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private Configuration.ISettings _settings;
        private IVsSolutionManager _solutionManager;
        private IDeleteOnRestartManager _deleteOnRestartManager;
        private INuGetTelemetryProvider _telemetryProvider;

        private JoinableTaskFactory PumpingJTF { get; }

        [ImportingConstructor]
        public VsPackageUninstaller(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            IVsSolutionManager solutionManager,
            IDeleteOnRestartManager deleteOnRestartManager,
            INuGetTelemetryProvider telemetryProvider)
        {
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _solutionManager = solutionManager;
            _telemetryProvider = telemetryProvider;

            PumpingJTF = new PumpingJTF(NuGetUIThreadHelper.JoinableTaskFactory);
            _deleteOnRestartManager = deleteOnRestartManager;
        }

        public void UninstallPackage(Project project, string packageId, bool removeDependencies)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageId)));
            }

            try
            {
                PumpingJTF.Run(async delegate
                    {
                        NuGetPackageManager packageManager =
                           new NuGetPackageManager(
                               _sourceRepositoryProvider,
                               _settings,
                               _solutionManager,
                               _deleteOnRestartManager);

                        var uninstallContext = new UninstallationContext(removeDependencies, forceRemove: false);
                        var projectContext = new VSAPIProjectContext
                        {
                            PackageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                PackageExtractionBehavior.XmlDocFileSaveMode,
                                ClientPolicyContext.GetClientPolicy(_settings, NullLogger.Instance),
                                NullLogger.Instance)
                        };

                        // find the project
                        NuGetProject nuGetProject = await _solutionManager.GetOrCreateProjectAsync(project, projectContext);

                        // uninstall the package
                        await packageManager.UninstallPackageAsync(nuGetProject, packageId, uninstallContext, projectContext, CancellationToken.None);
                    });
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsPackageUninstaller).FullName);
                throw;
            }
        }
    }
}
