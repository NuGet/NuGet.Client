// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Context of a PackageManagement UI window
    /// </summary>
    public sealed class NuGetUIContext : INuGetUIContext
    {
        private IProjectContextInfo[] _projects;

        public NuGetUIContext(
            ISourceRepositoryProvider sourceProvider,
            IVsSolutionManager solutionManager,
            INuGetSolutionManagerService solutionManagerService,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IUserSettingsManager userSettingsManager,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
        {
            SourceProvider = sourceProvider;
            SolutionManager = solutionManager;
            SolutionManagerService = solutionManagerService;
            PackageManager = packageManager;
            UIActionEngine = uiActionEngine;
            PackageManager = packageManager;
            PackageRestoreManager = packageRestoreManager;
            OptionsPageActivator = optionsPageActivator;
            UserSettingsManager = userSettingsManager;
            PackageManagerProviders = packageManagerProviders;
        }

        public ISourceRepositoryProvider SourceProvider { get; }

        public IVsSolutionManager SolutionManager { get; }

        public INuGetSolutionManagerService SolutionManagerService { get; }

        public NuGetPackageManager PackageManager { get; }

        public UIActionEngine UIActionEngine { get; }

        public IPackageRestoreManager PackageRestoreManager { get; }

        public IOptionsPageActivator OptionsPageActivator { get; }

        public IEnumerable<IProjectContextInfo> Projects
        {
            get { return _projects; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _projects = value.ToArray();
            }
        }

        public IUserSettingsManager UserSettingsManager { get; }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }

        public async Task<bool> IsNuGetProjectUpgradeableAsync(IProjectContextInfo project, CancellationToken cancellationToken)
        {
            return await project.IsUpgradeableAsync(cancellationToken);
        }

        public async Task<IModalProgressDialogSession> StartModalProgressDialogAsync(string caption, ProgressDialogData initialData, INuGetUI uiService)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var waitForDialogFactory = (IVsThreadedWaitDialogFactory)Package.GetGlobalService(typeof(SVsThreadedWaitDialogFactory));
            var progressData = new ThreadedWaitDialogProgressData(
                initialData.WaitMessage,
                initialData.ProgressText,
                null,
                initialData.IsCancelable,
                initialData.CurrentStep,
                initialData.TotalSteps);
            var session = waitForDialogFactory.StartWaitDialog(caption, progressData);
            return new VisualStudioProgressDialogSession(session);
        }

        public void Dispose()
        {
            SolutionManagerService.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
