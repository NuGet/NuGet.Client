// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
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
    [Export]
    public sealed class SolutionRestoreBuildHandler
        : IVsUpdateSolutionEvents5, IDisposable
    {
        private const uint VSCOOKIE_NIL = 0;

        private ISettings Settings { get; }

        private ISolutionRestoreWorker SolutionRestoreWorker { get; }

        private ISolutionRestoreChecker SolutionRestoreChecker { get; }

        /// <summary>
        /// The <see cref="IVsSolutionBuildManager3"/> object controlling the update solution events.
        /// </summary>
        private IVsSolutionBuildManager3 _solutionBuildManager;

        /// <summary>
        /// The cookie associated to the the <see cref="IVsUpdateSolutionEvents5"/> events.
        /// </summary>
        private uint _updateSolutionEventsCookieEx;

        [ImportingConstructor]
        internal SolutionRestoreBuildHandler(ISettings settings, ISolutionRestoreWorker restoreWorker, ISolutionRestoreChecker solutionRestoreChecker)
        {
            Assumes.Present(settings);
            Assumes.Present(restoreWorker);
            Assumes.Present(solutionRestoreChecker);

            Settings = settings;
            SolutionRestoreWorker = restoreWorker;
            SolutionRestoreChecker = solutionRestoreChecker;
        }

        // A constructor utilized for running unit-tests
        public SolutionRestoreBuildHandler(
            ISettings settings,
            ISolutionRestoreWorker restoreWorker,
            IVsSolutionBuildManager3 buildManager,
            ISolutionRestoreChecker solutionRestoreChecker)
        {
            Assumes.Present(settings);
            Assumes.Present(restoreWorker);
            Assumes.Present(buildManager);
            Assumes.Present(solutionRestoreChecker);

            Settings = settings;
            SolutionRestoreWorker = restoreWorker;
            SolutionRestoreChecker = solutionRestoreChecker;
            _solutionBuildManager = buildManager;
        }

        public void Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_updateSolutionEventsCookieEx != VSCOOKIE_NIL)
                {
                    ((IVsSolutionBuildManager)_solutionBuildManager).UnadviseUpdateSolutionEvents(_updateSolutionEventsCookieEx);
                    _updateSolutionEventsCookieEx = VSCOOKIE_NIL;
                }
            }).PostOnFailure(nameof(SolutionRestoreBuildHandler));
        }

        // A factory method invoked internally only
        internal async Task InitializeAsync(Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider)
        {
            Assumes.Present(serviceProvider);

            // Don't use CPS thread helper because of RPS perf regression
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _solutionBuildManager = await serviceProvider.GetServiceAsync<SVsSolutionBuildManager, IVsSolutionBuildManager3>();
            Assumes.Present(_solutionBuildManager);

            ((IVsSolutionBuildManager6)_solutionBuildManager).AdviseUpdateSolutionEventsEx(GuidList.guidNuGetSBMEvents, this, out _updateSolutionEventsCookieEx);
        }

        #region IVsUpdateSolutionEvents5

        public void UpdateSolution_QueryDelayBuildAction(uint dwAction, out IVsTask pDelayTask)
        {
            pDelayTask = SolutionRestoreWorker.JoinableTaskFactory.RunAsyncAsVsTask(
                VsTaskRunContext.UIThreadBackgroundPriority,
                async (token) => await RestoreAsync(dwAction, token));
        }

        #endregion IVsUpdateSolutionEvents5

        public async Task<bool> RestoreAsync(uint buildAction, CancellationToken token)
        {
            // move to bg thread to continue with restore
            await TaskScheduler.Default;

            if ((buildAction & (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN) != 0 &&
                (buildAction & (uint)VSSOLNBUILDUPDATEFLAGS3.SBF_FLAGS_UPTODATE_CHECK) == 0)
            {
                // Clear the transitive restore cache on clean to ensure that the next build restores again
                await SolutionRestoreWorker.CleanCacheAsync();
                SolutionRestoreChecker.CleanCache();
            }
            else if ((buildAction & (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD) != 0 &&
                    (buildAction & (uint)VSSOLNBUILDUPDATEFLAGS3.SBF_FLAGS_UPTODATE_CHECK) == 0 &&
                    ShouldRestoreOnBuild)
            {
                // start a restore task
                var forceRestore = (buildAction & (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE) != 0;

                var restoreTask = SolutionRestoreWorker.JoinableTaskFactory.RunAsync(() =>
                    SolutionRestoreWorker.RestoreAsync(
                        SolutionRestoreRequest.OnBuild(forceRestore),
                        token));

                // wait until restore is done which will block build without blocking UI thread
                return await restoreTask;
            }

            return true;
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
    }
}
