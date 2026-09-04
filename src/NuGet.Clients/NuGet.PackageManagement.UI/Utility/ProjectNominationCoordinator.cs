// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.SolutionRestoreManager;

namespace NuGet.PackageManagement.UI.Utility
{
    /// <summary>
    /// Waits for project-system nominations to settle before the Package Manager UI refreshes, so a
    /// multi-project operation (for example a Central Package Management install) that nominates many
    /// projects in succession produces a single refresh instead of a cancel/restart storm.
    /// <para>
    /// A project still owes a nomination when it reports <see cref="IVsProjectRestoreInfoSource.HasPendingNomination"/>;
    /// <see cref="IVsProjectRestoreInfoSource.WhenNominated"/> is awaited for those, re-scanning until a pass
    /// finds none pending. The wait is event-driven but bounded by two backstops so a misbehaving project
    /// system cannot hang the UI: a progress guard (stop rather than busy-spin when pending sources only report
    /// already-completed nominations) and a total time budget (<see cref="DefaultNominationSettleTimeout"/>).
    /// If a backstop trips we refresh with the best-available state; the next cache-updated notification
    /// self-heals any staleness.
    /// </para>
    /// <remarks>
    /// The <see cref="IVsProjectRestoreInfoSource"/> instances are in-process COM objects, so this must be
    /// invoked in the Visual Studio process. It is not safe across a service-broker boundary.
    /// </remarks>
    /// </summary>
    internal sealed class ProjectNominationCoordinator
    {
        // Total time budget for a single settle wait. A safety backstop only: the normal case settles in
        // milliseconds. Matches SolutionRestoreWorker.BulkRestoreCoordinationTimeout, the bound NuGet's own
        // restore worker applies to the same IVsProjectRestoreInfoSource.WhenNominated contract.
        internal static readonly TimeSpan DefaultNominationSettleTimeout = TimeSpan.FromMinutes(5);

        private readonly IVsSolutionManager _solutionManager;
        private readonly TimeSpan _nominationSettleTimeout;

        internal ProjectNominationCoordinator(IVsSolutionManager solutionManager)
            : this(solutionManager, DefaultNominationSettleTimeout)
        {
        }

        // Test hook: allows the settle-timeout backstop to be exercised without a multi-minute wait.
        internal ProjectNominationCoordinator(IVsSolutionManager solutionManager, TimeSpan nominationSettleTimeout)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _nominationSettleTimeout = nominationSettleTimeout;
        }

        /// <summary>
        /// Waits until no project reports a pending nomination.
        /// </summary>
        /// <param name="projectFullPath">
        /// When non-<see langword="null"/>, only the single project whose restore-info source matches this full path
        /// is awaited (project-level PM UI: the open project is the only one whose nominations trigger a refresh).
        /// When <see langword="null"/>, every project is awaited (solution-level PM UI, which refreshes on any
        /// project's nomination). The value is compared against <see cref="IVsProjectRestoreInfoSource.Name"/>,
        /// which is the project's full path (the same value the cache-updated notification carries).
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token. Cancelled when a newer nomination supersedes this wait or the control closes.
        /// </param>
        internal async Task WaitForNominationsToSettleAsync(string? projectFullPath, CancellationToken cancellationToken)
        {
            // Total budget for the entire settle wait, mirroring SolutionRestoreWorker.BulkRestoreCoordinationTimeout:
            // NuGet's own consumer of this contract bounds its WhenNominated waits so a slow or stuck project system
            // cannot block indefinitely. The budget is captured once and shared across re-scans. It is a safety
            // backstop only; the normal case settles in milliseconds. If it ever trips, we refresh with the
            // best-available cache state and the next cache-updated notification self-heals any staleness.
            DateTime waitStartTime = default;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Snapshot the projects that still owe a nomination and wait for all of them at once.
                IReadOnlyList<object> restoreInfoSources = _solutionManager.GetAllProjectRestoreInfoSources();
                List<Task>? pendingNominations = null;
                bool awaitedIncompleteNomination = false;

                foreach (object source in restoreInfoSources.NoAllocEnumerate())
                {
                    var restoreInfoSource = (IVsProjectRestoreInfoSource)source;

                    // In project-level PM UI, ignore other projects' nominations: only the open project refreshes.
                    if (projectFullPath != null
                        && !string.Equals(restoreInfoSource.Name, projectFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (restoreInfoSource.HasPendingNomination)
                    {
                        Task whenNominated = restoreInfoSource.WhenNominated(cancellationToken);
                        if (!whenNominated.IsCompleted)
                        {
                            awaitedIncompleteNomination = true;
                        }

                        (pendingNominations ??= new List<Task>()).Add(whenNominated);
                    }
                }

                // A full pass with no pending nominations means every project has settled.
                if (pendingNominations == null)
                {
                    return;
                }

                // Observe faults up front so no completed nomination task is left unobserved, regardless of which
                // return path we take below. A source cancels its nomination if it decides it no longer needs to
                // nominate, or faults it if its design-time build failed; either way the project is no longer
                // pending and we must not propagate a project-system fault into the UI refresh. The continuation
                // runs synchronously for already-completed tasks, so any fault is observed here and now.
                Task allNominations = Task.WhenAll(pendingNominations);
                Task observedNominations = allNominations.ContinueWith(
                    completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                // Progress guard: every source that reported a pending nomination already returned a completed
                // WhenNominated task. The contract says a pending source yields an incomplete task, so this means
                // the HasPendingNomination flags are lagging behind their (already finished) nominations. Awaiting
                // completed tasks cannot make progress and does not yield the calling thread, so re-scanning would
                // busy-spin (and freeze the UI thread). Treat this as settled; self-heal covers any real straggler.
                if (!awaitedIncompleteNomination)
                {
                    return;
                }

                if (waitStartTime == default)
                {
                    waitStartTime = DateTime.UtcNow;
                }

                TimeSpan remainingBudget = CalculateRemainingTimeout(waitStartTime, DateTime.UtcNow, _nominationSettleTimeout);

                // Race the settle wait against the remaining budget so a stuck nomination cannot block forever.
                // Cancel the timer when the nominations win so re-scans do not accumulate pending timers.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task timeoutTask = Task.Delay(remainingBudget, timeoutCts.Token);
                Task finished = await Task.WhenAny(observedNominations, timeoutTask);

                cancellationToken.ThrowIfCancellationRequested();

                if (finished == timeoutTask)
                {
                    // Budget exhausted: refresh with the best-available state rather than wait indefinitely.
                    return;
                }

                timeoutCts.Cancel();

                // Re-scan: a project may have started a new nomination while we awaited the others.
            }
        }

        /// <summary>
        /// Returns the time remaining in a total budget that began at <paramref name="startTime"/>, clamped to zero.
        /// Mirrors <c>SolutionRestoreWorker.CalculateTimeoutTime</c>.
        /// </summary>
        private static TimeSpan CalculateRemainingTimeout(DateTime startTime, DateTime currentTime, TimeSpan totalTimeout)
        {
            TimeSpan remaining = (startTime - currentTime) + totalTimeout;
            return remaining.Ticks > 0 ? remaining : TimeSpan.Zero;
        }
    }
}
