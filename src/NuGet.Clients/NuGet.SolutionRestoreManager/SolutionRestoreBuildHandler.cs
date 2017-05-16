// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.VisualStudio;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// A solution build manager events listener orchestrating on-build restore operations
    /// </summary>
    /// <remarks>
    /// Utilizes four core events to start, monitor, and control restore operations:
    /// UpdateSolution_Begin
    /// UpdateSolution_QueryDelayFirstUpdateAction
    /// UpdateSolution_Cancel
    /// UpdateSolution_Done
    /// </remarks>
    public sealed class SolutionRestoreBuildHandler 
        : IVsUpdateSolutionEvents4, IVsUpdateSolutionEvents2, IDisposable
    {
        private const uint REBUILD_FLAG = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE + (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
        private const uint VSCOOKIE_NIL = 0;

        [Import]
        private Lazy<INuGetLockService> LockService { get; set; }

        [Import]
        private Lazy<ISettings> Settings { get; set; }

        [Import]
        private Lazy<ISolutionRestoreWorker> SolutionRestoreWorker { get; set; }

        /// <summary>
        /// The <see cref="IVsSolutionBuildManager3"/> object controlling the update solution events.
        /// </summary>
        private IVsSolutionBuildManager3 _solutionBuildManager;

        /// <summary>
        /// The cookie associated to the the <see cref="IVsUpdateSolutionEvents4"/> events.
        /// </summary>
        private uint _updateSolutionEventsCookie4;

        /// <summary>
        /// The cookie associated to the the <see cref="IVsUpdateSolutionEvents2"/> events.
        /// </summary>
        private uint _updateSolutionEventsCookie2;

        private RestoreTask _restoreTask = RestoreTask.None;

        private SolutionRestoreBuildHandler()
        {
        }

        // A constructor utilized for running unit-tests
        public SolutionRestoreBuildHandler(
            INuGetLockService lockService,
            ISettings settings,
            ISolutionRestoreWorker restoreWorker,
            IVsSolutionBuildManager3 buildManager)
        {
            Assumes.Present(lockService);
            Assumes.Present(settings);
            Assumes.Present(restoreWorker);
            Assumes.Present(buildManager);

            LockService = new Lazy<INuGetLockService>(() => lockService);
            Settings = new Lazy<ISettings>(() => settings);
            SolutionRestoreWorker = new Lazy<ISolutionRestoreWorker>(() => restoreWorker);

            _solutionBuildManager = buildManager;
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_updateSolutionEventsCookie4 != VSCOOKIE_NIL)
            {
                ((IVsSolutionBuildManager5)_solutionBuildManager).UnadviseUpdateSolutionEvents4(_updateSolutionEventsCookie4);
                _updateSolutionEventsCookie4 = VSCOOKIE_NIL;
            }

            if (_updateSolutionEventsCookie2 != VSCOOKIE_NIL)
            {
                ((IVsSolutionBuildManager2)_solutionBuildManager).UnadviseUpdateSolutionEvents(_updateSolutionEventsCookie4);
                _updateSolutionEventsCookie2 = VSCOOKIE_NIL;
            }

            _restoreTask.Dispose();
        }

        // A factory method invoked internally only
        internal static async Task<IDisposable> InitializeAsync(Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider)
        {
            Assumes.Present(serviceProvider);

            var instance = new SolutionRestoreBuildHandler();

            var componentModel = await serviceProvider.GetComponentModelAsync();
            componentModel.DefaultCompositionService.SatisfyImportsOnce(instance);

            await instance.SubscribeAsync(serviceProvider);

            return instance;
        }

        private async Task SubscribeAsync(Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider)
        {
            // Don't use CPS thread helper because of RPS perf regression
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _solutionBuildManager = await serviceProvider.GetServiceAsync<SVsSolutionBuildManager, IVsSolutionBuildManager3>();
            Assumes.Present(_solutionBuildManager);

            ((IVsSolutionBuildManager5)_solutionBuildManager).AdviseUpdateSolutionEvents4(this, out _updateSolutionEventsCookie4);

            ErrorHandler.ThrowOnFailure(
                ((IVsSolutionBuildManager2)_solutionBuildManager).AdviseUpdateSolutionEvents(
                    this, out _updateSolutionEventsCookie2));
        }

        #region IVsUpdateSolutionEvents4

        public void UpdateSolution_QueryDelayFirstUpdateAction(out int pfDelay)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // check if NuGet lock is already acquired by some other NuGet operation
            if (LockService.Value.IsLockHeld)
            {
                // delay build by setting pfDelay to non-zero
                pfDelay = 10;
            }
            else if (!_restoreTask.IsCompleted)
            {
                // delay build by setting pfDelay to non-zero since restore is still running
                pfDelay = 10;
            }
            else
            {
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

        #endregion IVsUpdateSolutionEvents4

        #region IVsUpdateSolutionEvents2

        /// <summary>
        /// Called when the active project configuration for a project in the solution has changed.
        /// </summary>
        /// <param name="hierarchy">The project whose configuration has changed.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public int OnActiveProjectCfgChange(IVsHierarchy hierarchy)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called right before a project configuration begins to build.
        /// </summary>
        /// <param name="hierarchy">The project that is to be build.</param>
        /// <param name="configProject">A configuration project object.</param>
        /// <param name="configSolution">A configuration solution object.</param>
        /// <param name="action">The action taken.</param>
        /// <param name="cancel">A flag indicating cancel.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        /// <remarks>The values for the action are defined in the enum _SLNUPDACTION env\msenv\core\slnupd2.h</remarks>
        public int UpdateProjectCfg_Begin(IVsHierarchy hierarchy, IVsCfg configProject, IVsCfg configSolution, uint action, ref int cancel)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called right after a project configuration is finished building.
        /// </summary>
        /// <param name="hierarchy">The project that has finished building.</param>
        /// <param name="configProject">A configuration project object.</param>
        /// <param name="configSolution">A configuration solution object.</param>
        /// <param name="action">The action taken.</param>
        /// <param name="success">Flag indicating success.</param>
        /// <param name="cancel">Flag indicating cancel.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        /// <remarks>The values for the action are defined in the enum _SLNUPDACTION env\msenv\core\slnupd2.h</remarks>
        public int UpdateProjectCfg_Done(IVsHierarchy hierarchy, IVsCfg configProject, IVsCfg configSolution, uint action, int success, int cancel)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called before any build actions have begun. This is the last chance to cancel the build before any building begins.
        /// </summary>
        /// <param name="cancelUpdate">Flag indicating cancel update.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public int UpdateSolution_Begin(ref int cancelUpdate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Query build manager operation flag
            uint buildManagerOperation;
            ErrorHandler.ThrowOnFailure(
                _solutionBuildManager.QueryBuildManagerBusyEx(out buildManagerOperation));

            if (buildManagerOperation == (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN)
            {
                // Clear the project.json restore cache on clean to ensure that the next build restores again
                SolutionRestoreWorker.Value.CleanCache();

                return VSConstants.S_OK;
            }

            if (!ShouldRestoreOnBuild)
            {
                return VSConstants.S_OK;
            }

            // start a restore task
            var forceRestore = buildManagerOperation == REBUILD_FLAG;
            _restoreTask = RestoreTask.Start(SolutionRestoreWorker.Value, forceRestore);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when a build is being cancelled.
        /// </summary>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public int UpdateSolution_Cancel()
        {
            if (!_restoreTask.IsCompleted)
            {
                _restoreTask.Cancel();
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when a build is completed.
        /// </summary>
        /// <param name="succeeded">true if no update actions failed.</param>
        /// <param name="modified">true if any update action succeeded.</param>
        /// <param name="cancelCommand">true if update actions were canceled.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public int UpdateSolution_Done(int succeeded, int modified, int cancelCommand)
        {
            _restoreTask.Dispose();
            _restoreTask = RestoreTask.None;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called before the first project configuration is about to be built.
        /// </summary>
        /// <param name="cancelUpdate">A flag indicating cancel update.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public int UpdateSolution_StartUpdate(ref int cancelUpdate)
        {
            return VSConstants.S_OK;
        }

        #endregion IVsUpdateSolutionEvents2

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        private bool ShouldRestoreOnBuild
        {
            get
            {
                var packageRestoreConsent = new PackageRestoreConsent(Settings.Value);
                return packageRestoreConsent.IsAutomatic;
            }
        }

        private class RestoreTask : IDisposable
        {
            private CancellationTokenSource _cts;
            private Task _task;

            public bool IsCompleted => _task.IsCompleted;

            public TaskAwaiter GetAwaiter() => _task.GetAwaiter();

            public static RestoreTask None => new RestoreTask { _task = Task.CompletedTask };

            public static RestoreTask Start(ISolutionRestoreWorker worker, bool forceRestore)
            {
                var cts = new CancellationTokenSource();
                var task = worker
                    .JoinableTaskFactory
                    .RunAsync(() => worker.ScheduleRestoreAsync(
                        SolutionRestoreRequest.OnBuild(forceRestore),
                        cts.Token))
                    .Task;

                return new RestoreTask
                {
                    _cts = cts,
                    _task = task
                };
            }

            public void Cancel()
            {
                _cts?.Cancel();
            }

            public void Dispose()
            {
                _cts?.Dispose();
            }
        }
    }
}
