// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Context of a PackageManagement UI window
    /// </summary>
    public sealed class NuGetUIContext : INuGetUIContext
    {
        private readonly IServiceBroker _serviceBroker;
        private readonly NuGetSolutionManagerServiceWrapper _solutionManagerService;
        private IProjectContextInfo[] _projects;

        private NuGetUIContext(
            ISourceRepositoryProvider sourceProvider,
            IServiceBroker serviceBroker,
            IVsSolutionManager solutionManager,
            NuGetSolutionManagerServiceWrapper solutionManagerService,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IUserSettingsManager userSettingsManager,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
        {
            SourceProvider = sourceProvider;
            _serviceBroker = serviceBroker;
            SolutionManager = solutionManager;
            _solutionManagerService = solutionManagerService;
            PackageManager = packageManager;
            UIActionEngine = uiActionEngine;
            PackageManager = packageManager;
            PackageRestoreManager = packageRestoreManager;
            OptionsPageActivator = optionsPageActivator;
            UserSettingsManager = userSettingsManager;
            PackageManagerProviders = packageManagerProviders;

            _serviceBroker.AvailabilityChanged += OnAvailabilityChanged;
        }

        public ISourceRepositoryProvider SourceProvider { get; }

        public IVsSolutionManager SolutionManager { get; }

        public INuGetSolutionManagerService SolutionManagerService => _solutionManagerService;

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

        public void Dispose()
        {
            _serviceBroker.AvailabilityChanged -= OnAvailabilityChanged;
            _solutionManagerService.Dispose();

            GC.SuppressFinalize(this);
        }

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

        public static async ValueTask<NuGetUIContext> CreateAsync(
            ISourceRepositoryProvider sourceRepositoryProvider,
            ISettings settings,
            IVsSolutionManager solutionManager,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IUserSettingsManager userSettingsManager,
            IDeleteOnRestartManager deleteOnRestartManager,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            INuGetLockService lockService,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(sourceRepositoryProvider);
            Assumes.NotNull(settings);
            Assumes.NotNull(solutionManager);
            Assumes.NotNull(packageRestoreManager);
            Assumes.NotNull(optionsPageActivator);
            Assumes.NotNull(userSettingsManager);
            Assumes.NotNull(deleteOnRestartManager);
            Assumes.NotNull(packageManagerProviders);
            Assumes.NotNull(lockService);

            cancellationToken.ThrowIfCancellationRequested();

            IServiceBroker serviceBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();

            var solutionManagerService = new NuGetSolutionManagerServiceWrapper()
            {
                Service = await GetSolutionManagerServiceAsync(cancellationToken)
            };

            var packageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                settings,
                solutionManager,
                deleteOnRestartManager);

            var actionEngine = new UIActionEngine(
                sourceRepositoryProvider,
                packageManager,
                lockService);

            return new NuGetUIContext(
                sourceRepositoryProvider,
                serviceBroker,
                solutionManager,
                solutionManagerService,
                packageManager,
                actionEngine,
                packageRestoreManager,
                optionsPageActivator,
                userSettingsManager,
                packageManagerProviders);
        }

        private void OnAvailabilityChanged(object sender, BrokeredServicesChangedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    _solutionManagerService.Service = await GetSolutionManagerServiceAsync(CancellationToken.None);
                })
                .PostOnFailure(nameof(NuGetUIContext), nameof(OnAvailabilityChanged));
        }

        private static async ValueTask<INuGetSolutionManagerService> GetSolutionManagerServiceAsync(CancellationToken cancellationToken)
        {
            IServiceBroker serviceBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();

#pragma warning disable ISB001 // Dispose of proxies
            return await serviceBroker.GetProxyAsync<INuGetSolutionManagerService>(
                NuGetServices.SolutionManagerService,
                cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies
        }
    }
}
