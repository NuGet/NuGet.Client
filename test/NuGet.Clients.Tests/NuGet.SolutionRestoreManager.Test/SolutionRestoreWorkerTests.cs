// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
                token: CancellationToken.None);

            await Task.Delay(50);
            isAllProjectsNominatedCallCount.Should().Be(0);

            solutionLoadCompleted.SetResult(true);

            SolutionRestoreWorker.RestoreReadinessResult readiness = await coordinationTask;
            readiness.RestoreReason.Should().Be(ImplicitRestoreReason.ProjectsReady);
            readiness.ProjectsReadyCheckCount.Should().Be(1);
            isAllProjectsNominatedCallCount.Should().Be(1);
        }
    }
}
