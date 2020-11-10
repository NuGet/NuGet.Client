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
using NuGet.ProjectManagement;
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
        private readonly NuGetSolutionManagerServiceWrapper _solutionManagerService;
        private readonly NuGetSourcesServiceWrapper _sourceService;
        private IProjectContextInfo[] _projects;

        public event EventHandler<IReadOnlyCollection<string>> ProjectActionsExecuted;

        // Non-private only to facilitate testing.
        internal NuGetUIContext(
            IServiceBroker serviceBroker,
            IVsSolutionManager solutionManager,
            NuGetSolutionManagerServiceWrapper solutionManagerService,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IUserSettingsManager userSettingsManager,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            NuGetSourcesServiceWrapper sourceService)
        {
            ServiceBroker = serviceBroker;
            SolutionManager = solutionManager;
            _solutionManagerService = solutionManagerService;
            PackageManager = packageManager;
            UIActionEngine = uiActionEngine;
            PackageManager = packageManager;
            PackageRestoreManager = packageRestoreManager;
            OptionsPageActivator = optionsPageActivator;
            UserSettingsManager = userSettingsManager;
            PackageManagerProviders = packageManagerProviders;
            _sourceService = sourceService;

            ServiceBroker.AvailabilityChanged += OnAvailabilityChanged;
            SolutionManager.ActionsExecuted += OnActionsExecuted;
        }

        public IServiceBroker ServiceBroker { get; }

        public IVsSolutionManager SolutionManager { get; }

        public INuGetSolutionManagerService SolutionManagerService => _solutionManagerService;

        public INuGetSourcesService SourceService => _sourceService;

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
            ServiceBroker.AvailabilityChanged -= OnAvailabilityChanged;
            SolutionManager.ActionsExecuted -= OnActionsExecuted;

            _solutionManagerService.Dispose();
            _sourceService.Dispose();

            GC.SuppressFinalize(this);
        }

        public async Task<bool> IsNuGetProjectUpgradeableAsync(IProjectContextInfo project, CancellationToken cancellationToken)
        {
            return await project.IsUpgradeableAsync(ServiceBroker, cancellationToken);
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
            IServiceBroker serviceBroker,
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
            Assumes.NotNull(serviceBroker);
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

            var solutionManagerServiceWrapper = new NuGetSolutionManagerServiceWrapper();
            INuGetSolutionManagerService solutionManagerService = await GetSolutionManagerServiceAsync(serviceBroker, cancellationToken);

            // The initial Swap(...) should return a null implementation of the interface that does not require disposal.
            // However, there's no harm in following form.
            using (solutionManagerServiceWrapper.Swap(solutionManagerService))
            {
            }

            var sourceServiceWrapper = new NuGetSourcesServiceWrapper();
            INuGetSourcesService sourceService = await GetSourceServiceAsync(serviceBroker, cancellationToken);
            using (sourceServiceWrapper.Swap(sourceService))
            {
            }

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
                serviceBroker,
                solutionManager,
                solutionManagerServiceWrapper,
                packageManager,
                actionEngine,
                packageRestoreManager,
                optionsPageActivator,
                userSettingsManager,
                packageManagerProviders,
                sourceServiceWrapper);
        }

        public void RaiseProjectActionsExecuted(IReadOnlyCollection<string> projectIds)
        {
            Assumes.NotNullOrEmpty(projectIds);

            ProjectActionsExecuted?.Invoke(this, projectIds);
        }

        private void OnActionsExecuted(object sender, ActionsExecutedEventArgs e)
        {
            Assumes.NotNull(e);

            if (e.Actions == null)
            {
                return;
            }

            string[] projectIds = e.Actions
                .Select(action => action.Project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId))
                .Distinct()
                .ToArray();

            if (projectIds.Length > 0)
            {
                RaiseProjectActionsExecuted(projectIds);
            }
        }

        private void OnAvailabilityChanged(object sender, BrokeredServicesChangedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    INuGetSolutionManagerService newService = await GetSolutionManagerServiceAsync(
                        ServiceBroker,
                        CancellationToken.None);

                    using (_solutionManagerService.Swap(newService))
                    {
                    }

                    INuGetSourcesService newSourceService = await GetSourceServiceAsync(
                        ServiceBroker,
                        CancellationToken.None);

                    using (_sourceService.Swap(newSourceService))
                    {
                    }
                })
                .PostOnFailure(nameof(NuGetUIContext), nameof(OnAvailabilityChanged));
        }

        private static async ValueTask<INuGetSourcesService> GetSourceServiceAsync(
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
#pragma warning disable ISB001 // Dispose of proxies
            INuGetSourcesService sourceService = await serviceBroker.GetProxyAsync<INuGetSourcesService>(
                NuGetServices.SourceProviderService,
                cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies

            Assumes.NotNull(sourceService);

            return sourceService;
        }

        private static async ValueTask<INuGetSolutionManagerService> GetSolutionManagerServiceAsync(
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
#pragma warning disable ISB001 // Dispose of proxies
            INuGetSolutionManagerService solutionManagerService = await serviceBroker.GetProxyAsync<INuGetSolutionManagerService>(
                NuGetServices.SolutionManagerService,
                cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies

            Assumes.NotNull(solutionManagerService);

            return solutionManagerService;
        }
    }
}
