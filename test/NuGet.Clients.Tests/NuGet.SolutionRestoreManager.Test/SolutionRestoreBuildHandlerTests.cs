// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Configuration;
using NuGet.VisualStudio;
using Test.Utility.Threading;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class SolutionRestoreBuildHandlerTests
    {
        private readonly JoinableTaskFactory _jtf;

        public SolutionRestoreBuildHandlerTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            _jtf = fixture.JoinableTaskFactory;
        }

        [Fact]
        public async Task Begin_OnCleanBuild_CleansCacheAndNoOps()
        {
            var lockService = Mock.Of<INuGetLockService>();
            var settings = Mock.Of<ISettings>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                // Act
                var cancelUpdate = 0;
                var hr = handler.UpdateSolution_Begin(ref cancelUpdate);

                Assert.Equal(VSConstants.S_OK, hr);
                Assert.Equal(0, cancelUpdate);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.CleanCache(), Times.Once);

            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Begin_ShouldNotRestoreOnBuild_NoOps()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.FalseString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                // Act
                var cancelUpdate = 0;
                var hr = handler.UpdateSolution_Begin(ref cancelUpdate);

                Assert.Equal(VSConstants.S_OK, hr);
                Assert.Equal(0, cancelUpdate);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Begin_ShouldRestoreOnBuild_StartsRestoreTask()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                // Act
                var cancelUpdate = 0;
                var hr = handler.UpdateSolution_Begin(ref cancelUpdate);

                Assert.Equal(VSConstants.S_OK, hr);
                Assert.Equal(0, cancelUpdate);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenIsLockHeld_Delays()
        {
            var lockService = Mock.Of<INuGetLockService>();
            Mock.Get(lockService)
                .SetupGet(x => x.IsLockHeld)
                .Returns(true);

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.FalseString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.NotEqual(0, delayMs);
            }
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenTaskIsNotCompleted_Delays()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.NotEqual(0, delayMs);
            }
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenTaskIsCompleted_DoesNotDelay()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                tcs.SetResult(true);

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.Equal(0, delayMs);
            }
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenNoRestoreScheduled_DoesNotDelay()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.FalseString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.Equal(0, delayMs);
            }
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenTaskIsCanceled_DoesNotDelay()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                tcs.SetCanceled();

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.Equal(0, delayMs);
            }
        }

        [Fact]
        public async Task QueryDelayFirstUpdateAction_WhenTaskHasFailed_DoesNotDelay()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                tcs.SetException(new InvalidOperationException());

                // Act
                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);

                Assert.Equal(0, delayMs);
            }
        }

        [Fact]
        public async Task Cancel_Always_CancelsRestoreTask()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            var restoreHasBeenCancelled = false;
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task)
                .Callback<SolutionRestoreRequest, CancellationToken>(
                    (r, t) => t.Register(() => restoreHasBeenCancelled = true));

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                // Act
                var hr = handler.UpdateSolution_Cancel();

                Assert.Equal(VSConstants.S_OK, hr);
                Assert.True(restoreHasBeenCancelled);
            }
        }

        [Fact]
        public async Task Done_Always_ResetsRestoreTask()
        {
            var lockService = Mock.Of<INuGetLockService>();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("packageRestore", "automatic", false))
                .Returns(bool.TrueString);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            var tcs = new TaskCompletionSource<bool>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildManagerOperation = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;
            Mock.Get(buildManager)
                .Setup(x => x.QueryBuildManagerBusyEx(out buildManagerOperation))
                .Returns(VSConstants.S_OK);

            using (var handler = new SolutionRestoreBuildHandler(lockService, settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var cancelUpdate = 0;
                handler.UpdateSolution_Begin(ref cancelUpdate);

                // Act
                var hr = handler.UpdateSolution_Done(succeeded: 1, modified: 1, cancelCommand:0);

                Assert.Equal(VSConstants.S_OK, hr);

                int delayMs;
                handler.UpdateSolution_QueryDelayFirstUpdateAction(out delayMs);
                Assert.Equal(0, delayMs);
            }
        }
    }
}
