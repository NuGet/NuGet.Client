// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class SolutionRestoreWorkerTests
    {
        [Fact]
        public void CalculateTimeoutTime_WithTimeoutLargerThanTimeElapsed_ReturnsPositiveValue()
        {
            var startTime = new DateTime(year: 2021, month: 7, day: 21, hour: 10, minute: 5, second: 20);
            var currentTime = new DateTime(year: 2021, month: 7, day: 21, hour: 10, minute: 7, second: 00);
            TimeSpan timeoutSpan = new(hours: 0, minutes: 5, seconds: 0);

            var timeout = SolutionRestoreWorker.CalculateTimeoutTime(startTime: startTime, currentTime: currentTime, timeoutTime: timeoutSpan);
            timeout.TotalMilliseconds.Should().Be(200000);
        }

        [Fact]
        public void CalculateTimeoutTime_WithTimeElapsedLargerThanTimeout_Returns0()
        {
            var startTime = new DateTime(year: 2021, month: 7, day: 21, hour: 10, minute: 5, second: 20);
            var currentTime = new DateTime(year: 2021, month: 7, day: 21, hour: 11, minute: 0, second: 00);
            TimeSpan timeoutSpan = new(hours: 0, minutes: 5, seconds: 0);

            var timeout = SolutionRestoreWorker.CalculateTimeoutTime(startTime: startTime, currentTime: currentTime, timeoutTime: timeoutSpan);
            timeout.TotalMilliseconds.Should().Be(0);
        }

        [Fact]
        public async Task WaitForOnBuildRestoreReadinessCoreAsync_WhenProjectHasPendingNomination_WaitsUntilNominated()
        {
            var whenNominatedStarted = new TaskCompletionSource<bool>();
            var whenNominatedCompleted = new TaskCompletionSource<bool>();
            var checkProjectsReadyCallCount = 0;

            Task<SolutionRestoreWorker.RestoreReadinessResult> coordinationTask = SolutionRestoreWorker.WaitForOnBuildRestoreReadinessCoreAsync(
                waitForSolutionLoadedAsync: _ => Task.CompletedTask,
                isAllProjectsNominatedAsync: () => Task.FromResult(true),
                checkProjectsReadyAsync: async (bulkRestoreCoordinationCheckStartTime, cancellationToken) =>
                {
                    checkProjectsReadyCallCount++;
                    if (checkProjectsReadyCallCount == 1)
                    {
                        whenNominatedStarted.TrySetResult(true);
                        await whenNominatedCompleted.Task;
                        return (false, false, 1, TimeSpan.FromMilliseconds(10));
                    }

                    return (true, false, 1, TimeSpan.FromMilliseconds(5));
                },
                getProjectRestoreInfoSourceCounts: () => (1, 1),
                delayAsync: (delay, cancellationToken) => Task.Delay(delay, cancellationToken),
                getUtcNow: () => DateTime.UtcNow,
                token: CancellationToken.None);

            await whenNominatedStarted.Task;
            coordinationTask.IsCompleted.Should().BeFalse();

            whenNominatedCompleted.SetResult(true);

            SolutionRestoreWorker.RestoreReadinessResult readiness = await coordinationTask;
            readiness.RestoreReason.Should().Be(ImplicitRestoreReason.ProjectsReady);
            readiness.ProjectsReadyCheckCount.Should().Be(2);
            readiness.ProjectRestoreInfoSourcesCount.Should().Be(1);
            readiness.ProjectReadyTimings.Should().HaveCount(2);
        }

        [Fact]
        public async Task WaitForOnBuildRestoreReadinessCoreAsync_WhenSolutionLoadCompletes_ThenChecksNomination()
        {
            var solutionLoadCompleted = new TaskCompletionSource<bool>();
            int isAllProjectsNominatedCallCount = 0;

            Task<SolutionRestoreWorker.RestoreReadinessResult> coordinationTask = SolutionRestoreWorker.WaitForOnBuildRestoreReadinessCoreAsync(
                waitForSolutionLoadedAsync: _ => solutionLoadCompleted.Task,
                isAllProjectsNominatedAsync: () =>
                {
                    Interlocked.Increment(ref isAllProjectsNominatedCallCount);
                    return Task.FromResult(true);
                },
                checkProjectsReadyAsync: (bulkRestoreCoordinationCheckStartTime, cancellationToken) =>
                    Task.FromResult((true, false, 0, TimeSpan.Zero)),
                getProjectRestoreInfoSourceCounts: () => (0, 0),
                delayAsync: (delay, cancellationToken) => Task.Delay(delay, cancellationToken),
                getUtcNow: () => DateTime.UtcNow,
                token: CancellationToken.None);

            await Task.Delay(50);
            isAllProjectsNominatedCallCount.Should().Be(0);

            solutionLoadCompleted.SetResult(true);

            SolutionRestoreWorker.RestoreReadinessResult readiness = await coordinationTask;
            readiness.RestoreReason.Should().Be(ImplicitRestoreReason.ProjectsReady);
            readiness.ProjectsReadyCheckCount.Should().Be(1);
            isAllProjectsNominatedCallCount.Should().Be(1);
        }

        [Fact]
        public async Task WaitForOnBuildRestoreReadinessCoreAsync_WhenReadinessTimesOutBeforeNominations_ReturnsTimeoutReason()
        {
            var utcNow = new DateTime(year: 2026, month: 9, day: 11);

            SolutionRestoreWorker.RestoreReadinessResult readiness = await SolutionRestoreWorker.WaitForOnBuildRestoreReadinessCoreAsync(
                waitForSolutionLoadedAsync: _ => Task.CompletedTask,
                isAllProjectsNominatedAsync: () => Task.FromResult(false),
                checkProjectsReadyAsync: (bulkRestoreCoordinationCheckStartTime, cancellationToken) =>
                    throw new InvalidOperationException("Projects cannot be ready before all projects are nominated."),
                getProjectRestoreInfoSourceCounts: () => (1, 1),
                delayAsync: (delay, cancellationToken) =>
                {
                    utcNow += TimeSpan.FromMinutes(6);
                    return Task.CompletedTask;
                },
                getUtcNow: () => utcNow,
                token: CancellationToken.None);

            readiness.RestoreReason.Should().Be(ImplicitRestoreReason.ProjectsReadyCheckTimeout);
            readiness.BulkRestoreCoordinationCheckStartTime.Should().NotBeNull();
            readiness.ProjectsReadyCheckCount.Should().Be(0);
            readiness.ProjectRestoreInfoSourcesCount.Should().Be(1);
            readiness.PendingProjectRestoreInfoSourcesCount.Should().Be(1);
            readiness.ProjectReadyTimings.Should().BeEmpty();
        }

        [Fact]
        public async Task WaitForOnBuildRestoreReadinessCoreAsync_WhenCanceledWhileWaitingForNominations_Throws()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            Func<Task> act = async () => await SolutionRestoreWorker.WaitForOnBuildRestoreReadinessCoreAsync(
                waitForSolutionLoadedAsync: _ => Task.CompletedTask,
                isAllProjectsNominatedAsync: () => Task.FromResult(false),
                checkProjectsReadyAsync: (bulkRestoreCoordinationCheckStartTime, cancellationToken) =>
                    throw new InvalidOperationException("Projects cannot be ready before all projects are nominated."),
                getProjectRestoreInfoSourceCounts: () => (1, 1),
                delayAsync: (delay, cancellationToken) =>
                {
                    cancellationTokenSource.Cancel();
                    return Task.FromCanceled(cancellationToken);
                },
                getUtcNow: () => DateTime.UtcNow,
                token: cancellationTokenSource.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task WaitForOnBuildRestoreReadinessCoreAsync_WhenLargeSolutionMakesProgress_DoesNotTimeOut()
        {
            var utcNow = new DateTime(year: 2026, month: 9, day: 11);
            int pendingSources = 3;

            SolutionRestoreWorker.RestoreReadinessResult readiness = await SolutionRestoreWorker.WaitForOnBuildRestoreReadinessCoreAsync(
                waitForSolutionLoadedAsync: _ => Task.CompletedTask,
                isAllProjectsNominatedAsync: () => Task.FromResult(pendingSources == 0),
                checkProjectsReadyAsync: (bulkRestoreCoordinationCheckStartTime, cancellationToken) =>
                    Task.FromResult((true, false, 3, TimeSpan.Zero)),
                getProjectRestoreInfoSourceCounts: () => (3, pendingSources),
                delayAsync: (delay, cancellationToken) =>
                {
                    utcNow += TimeSpan.FromMinutes(4);
                    pendingSources--;
                    return Task.CompletedTask;
                },
                getUtcNow: () => utcNow,
                token: CancellationToken.None);

            readiness.RestoreReason.Should().Be(ImplicitRestoreReason.ProjectsReady);
            readiness.PendingProjectRestoreInfoSourcesCount.Should().Be(0);
            DateTime coordinationStartTime = readiness.BulkRestoreCoordinationCheckStartTime.GetValueOrDefault();
            coordinationStartTime.Should().NotBe(default);
            (utcNow - coordinationStartTime).Should().Be(TimeSpan.FromMinutes(12));
        }

        [Fact]
        public async Task CheckProjectsReadyCoreAsync_WhenSourceHasPendingNomination_WaitsForNomination()
        {
            var nominationCompleted = new TaskCompletionSource<bool>();
            var timeoutTask = new TaskCompletionSource<bool>();
            var restoreInfoSource = new Mock<IVsProjectRestoreInfoSource>();
            restoreInfoSource
                .SetupGet(x => x.HasPendingNomination)
                .Returns(true);
            restoreInfoSource
                .Setup(x => x.WhenNominated(It.IsAny<CancellationToken>()))
                .Returns(nominationCompleted.Task);

            Task<(bool allProjectsReady, bool bulkCheckTimeout, int projectRestoreInfoSourcesCount, TimeSpan projectReadyCheckTime)> readinessTask =
                SolutionRestoreWorker.CheckProjectsReadyCoreAsync(
                    restoreProjectInfoSources: new object[] { restoreInfoSource.Object },
                    bulkRestoreCoordinationCheckStartTime: DateTime.UtcNow,
                    getUtcNow: () => DateTime.UtcNow,
                    delayAsync: (delay, cancellationToken) => timeoutTask.Task,
                    token: CancellationToken.None);

            readinessTask.IsCompleted.Should().BeFalse();

            nominationCompleted.SetResult(true);

            (bool allProjectsReady, bool bulkCheckTimeout, int projectRestoreInfoSourcesCount, _) = await readinessTask;
            allProjectsReady.Should().BeFalse();
            bulkCheckTimeout.Should().BeFalse();
            projectRestoreInfoSourcesCount.Should().Be(1);
        }

        [Fact]
        public async Task CheckProjectsReadyCoreAsync_WhenSourcesMakeProgressLongerThanTimeout_DoesNotTimeOut()
        {
            var utcNow = new DateTime(year: 2026, month: 9, day: 11);
            var restoreInfoSources = new object[3];
            for (int i = 0; i < restoreInfoSources.Length; i++)
            {
                var restoreInfoSource = new Mock<IVsProjectRestoreInfoSource>();
                restoreInfoSource
                    .SetupGet(x => x.HasPendingNomination)
                    .Returns(true);
                restoreInfoSource
                    .Setup(x => x.WhenNominated(It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        utcNow += TimeSpan.FromMinutes(4);
                        return Task.CompletedTask;
                    });
                restoreInfoSources[i] = restoreInfoSource.Object;
            }

            (bool allProjectsReady, bool bulkCheckTimeout, int projectRestoreInfoSourcesCount, _) =
                await SolutionRestoreWorker.CheckProjectsReadyCoreAsync(
                    restoreProjectInfoSources: restoreInfoSources,
                    bulkRestoreCoordinationCheckStartTime: utcNow,
                    getUtcNow: () => utcNow,
                    delayAsync: (delay, cancellationToken) => new TaskCompletionSource<bool>().Task,
                    token: CancellationToken.None);

            allProjectsReady.Should().BeFalse();
            bulkCheckTimeout.Should().BeFalse();
            projectRestoreInfoSourcesCount.Should().Be(3);
        }
    }
}
