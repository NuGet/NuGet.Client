// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Visual Studio extension package designed to bootstrap solution restore components.
    /// Loads on solution open to attach to build events.
    /// </summary>
    // Flag AllowsBackgroundLoading is set to True and Flag PackageAutoLoadFlags is set to BackgroundLoad
    // which will allow this package to be loaded asynchronously
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class RestoreManagerPackage : AsyncPackage
    {
        public const string ProductVersion = "4.0.0";

        /// <summary>
        /// RestoreManagerPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "2b52ac92-4551-426d-bd34-c6d7d9fdd1c5";

        private Lazy<ISolutionRestoreWorker> _restoreWorker;
        private Lazy<ISettings> _settings;
        private Lazy<IVsSolutionManager> _solutionManager;

        private ISolutionRestoreWorker SolutionRestoreWorker => _restoreWorker.Value;
        private ISettings Settings => _settings.Value;
        private IVsSolutionManager SolutionManager => _solutionManager.Value;

        private IVsSolutionBuildManager5 _solutionBuildManager;

        private IVsSolutionBuildManager3 _solutionBuildManager3;

        private uint _updateSolutionEventsCookie4;

        private uint _updateSolutionEventsCookie3;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            componentModel.DefaultCompositionService.SatisfyImportsOnce(this);

            _restoreWorker = new Lazy<ISolutionRestoreWorker>(
                () => componentModel.GetService<ISolutionRestoreWorker>());

            _settings = new Lazy<ISettings>(
                () => componentModel.GetService<ISettings>());

            _solutionManager = new Lazy<IVsSolutionManager>(
                () => componentModel.GetService<IVsSolutionManager>());

            var lockService = new Lazy<INuGetLockService>(
                () => componentModel.GetService<INuGetLockService>());

            var updateSolutionEvent = new VsUpdateSolutionEvent(lockService, this);

            // Don't use CPS thread helper because of RPS perf regression
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = (EnvDTE.DTE)await GetServiceAsync(typeof(SDTE));

                UserAgent.SetUserAgentString(
                    new UserAgentStringBuilder().WithVisualStudioSKU(dte.GetFullVsVersionString()));

                _solutionBuildManager = (IVsSolutionBuildManager5)await GetServiceAsync(typeof(SVsSolutionBuildManager));
                Assumes.Present(_solutionBuildManager);

                _solutionBuildManager.AdviseUpdateSolutionEvents4(updateSolutionEvent, out _updateSolutionEventsCookie4);

                _solutionBuildManager3 = (IVsSolutionBuildManager3)await GetServiceAsync(typeof(SVsSolutionBuildManager));
                Assumes.Present(_solutionBuildManager3);

                _solutionBuildManager3.AdviseUpdateSolutionEvents3(updateSolutionEvent, out _updateSolutionEventsCookie3);
            });

            await SolutionRestoreCommand.InitializeAsync(this);

            await base.InitializeAsync(cancellationToken, progress);
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_updateSolutionEventsCookie4 != 0)
            {
                _solutionBuildManager?.UnadviseUpdateSolutionEvents4(_updateSolutionEventsCookie4);
            }

            if (_updateSolutionEventsCookie3 != 0)
            {
                _solutionBuildManager3?.UnadviseUpdateSolutionEvents3(_updateSolutionEventsCookie3);
            }
        }

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        private bool ShouldRestoreOnBuild
        {
            get
            {
                var packageRestoreConsent = new PackageRestoreConsent(Settings);
                return packageRestoreConsent.IsAutomatic;
            }
        }

        private sealed class VsUpdateSolutionEvent : IVsUpdateSolutionEvents4, IVsUpdateSolutionEvents3
        {
            private const uint REBUILD_FLAG = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE + (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;

            private Lazy<INuGetLockService> _lockService;

            private RestoreManagerPackage _restoreManagerPackage;

            private Task _restoreTask = Task.CompletedTask;

            private bool _issRestoreRunning = false;

            public VsUpdateSolutionEvent(Lazy<INuGetLockService> lockService, RestoreManagerPackage restoreManagerPackage)
            {
                Assumes.NotNull(restoreManagerPackage);

                _restoreManagerPackage = restoreManagerPackage;
                _lockService = lockService;
            }

            private void OnBuildRestore()
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Query build manager operation flag
                uint buildManagerOperation;
                _restoreManagerPackage._solutionBuildManager3.QueryBuildManagerBusyEx(out buildManagerOperation);

                if (buildManagerOperation == (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN)
                {
                    // Clear the project.json restore cache on clean to ensure that the next build restores again
                    _restoreManagerPackage.SolutionRestoreWorker.CleanCache();

                    return;
                }

                if (!_restoreManagerPackage.ShouldRestoreOnBuild)
                {
                    return;
                }

                var forceRestore = buildManagerOperation == REBUILD_FLAG;

                // start a restore task
                if (_restoreTask.IsCompleted)
                {
                    _restoreTask = NuGetUIThreadHelper.JoinableTaskFactory
                        .RunAsync(() => _restoreManagerPackage.SolutionRestoreWorker.ScheduleRestoreAsync(
                            SolutionRestoreRequest.OnBuild(forceRestore),
                            CancellationToken.None))
                        .Task;
                }
            }

            public void UpdateSolution_QueryDelayFirstUpdateAction(out int pfDelay)
            {
                // check if NuGet lock is already acquired by some other NuGet operation
                if (_lockService.Value.IsLockHeld)
                {
                    // delay build by setting pfDelay to non-zero
                    pfDelay = 1;
                }
                // check if no build restore is running, then start a new one
                else if (!_issRestoreRunning)
                {
                    // disable running more build restore
                    _issRestoreRunning = true;

                    // run build restore
                    OnBuildRestore();

                    // delay build until restore is running
                    pfDelay = 1;

                }
                else if (!_restoreTask.IsCompleted)
                {
                    // delay build by setting pfDelay to non-zero since restore is still running
                    pfDelay = 1;
                }
                else
                {
                    // enable running build restore again.
                    _issRestoreRunning = false;

                    // Set delay to 0 which means allow build to proceed.
                    pfDelay = 0;
                }
            }

            public void UpdateSolution_BeginFirstUpdateAction() { }

            public void UpdateSolution_EndLastUpdateAction() { }

            public void UpdateSolution_BeginUpdateAction(uint dwAction) { }

            public void UpdateSolution_EndUpdateAction(uint dwAction) { }

            public void OnActiveProjectCfgChangeBatchBegin() { }

            public void OnActiveProjectCfgChangeBatchEnd() { }

            public int OnBeforeActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg)
            {
                return VSConstants.S_OK;
            }

            public int OnAfterActiveSolutionCfgChange(IVsCfg pOldActiveSlnCfg, IVsCfg pNewActiveSlnCfg)
            {
                return VSConstants.S_OK;
            }
        }
    }
}
