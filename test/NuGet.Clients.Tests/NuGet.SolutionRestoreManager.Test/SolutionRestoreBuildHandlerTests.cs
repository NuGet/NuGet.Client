// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft;
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
        public async Task QueryDelayBuildAction_CleanBuild()
        {
            var settings = Mock.Of<ISettings>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            var buildManager = Mock.Of<IVsSolutionBuildManager3>();
            var buildAction = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_CLEAN;

            using (var handler = new SolutionRestoreBuildHandler(settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var result = await handler.RestoreAsync(buildAction, CancellationToken.None);

                Assert.True(result);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.CleanCacheAsync(), Times.Once);
 
            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryDelayBuildAction_ShouldNotRestoreOnBuild_NoOps()
        {
            var settings = Mock.Of<ISettings>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            var buildManager = Mock.Of<IVsSolutionBuildManager3>();

            var buildAction = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;

            Mock.Get(settings)
                .Setup(x => x.GetSection("packageRestore"))
                .Returns(() => new SettingsSection("packageRestore",
                    new AddItem("automatic", bool.FalseString)));

            using (var handler = new SolutionRestoreBuildHandler(settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var result = await handler.RestoreAsync(buildAction, CancellationToken.None);

                Assert.True(result);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryDelayBuildAction_ShouldNotRestoreOnBuild_ProjectUpToDateMark()
        {
            var settings = Mock.Of<ISettings>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            var buildManager = Mock.Of<IVsSolutionBuildManager3>();

            var buildAction = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD + (uint)VSSOLNBUILDUPDATEFLAGS3.SBF_FLAGS_UPTODATE_CHECK;

            Mock.Get(settings)
                .Setup(x => x.GetSection("packageRestore"))
                .Returns(() => new SettingsSection("packageRestore",
                    new AddItem("automatic", bool.TrueString)));

            using (var handler = new SolutionRestoreBuildHandler(settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var result = await handler.RestoreAsync(buildAction, CancellationToken.None);

                Assert.True(result);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueryDelayBuildAction_ShouldRestoreOnBuild()
        {
            var settings = Mock.Of<ISettings>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            var buildManager = Mock.Of<IVsSolutionBuildManager3>();

            var buildAction = (uint)VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD;

            Mock.Get(settings)
                .Setup(x => x.GetSection("packageRestore"))
                .Returns(() => new SettingsSection("packageRestore",
                    new AddItem("automatic", bool.TrueString)));
            Mock.Get(restoreWorker)
                .SetupGet(x => x.JoinableTaskFactory)
                .Returns(_jtf);
            Mock.Get(restoreWorker)
                .Setup(x => x.RestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            using (var handler = new SolutionRestoreBuildHandler(settings, restoreWorker, buildManager))
            {
                await _jtf.SwitchToMainThreadAsync();

                var result = await handler.RestoreAsync(buildAction, CancellationToken.None);

                Assert.True(result);
            }

            Mock.Get(restoreWorker)
                .Verify(x => x.RestoreAsync(It.IsAny<SolutionRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

    }
}
