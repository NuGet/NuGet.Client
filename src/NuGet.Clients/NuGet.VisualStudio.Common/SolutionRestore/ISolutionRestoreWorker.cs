// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Coordinates solution restore jobs execution.
    /// </summary>
    public interface ISolutionRestoreWorker
    {
        /// <summary>
        /// Returns currently running or last completed restore operation's result.
        /// </summary>
        Task<bool> CurrentRestoreOperation { get; }

        /// <summary>
        /// Returns true when it's executing a restore operation.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Joinable task factory to syncronize with the worker.
        /// </summary>
        JoinableTaskFactory JoinableTaskFactory { get; }

        /// <summary>
        /// Schedules backgroud restore operation.
        /// </summary>
        /// <param name="request">Restore request.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that completes when scheduled operation completes.</returns>
        Task<bool> ScheduleRestoreAsync(SolutionRestoreRequest request, CancellationToken token);

        /// <summary>
        /// Blocking call to run solution restore job.
        /// </summary>
        /// <param name="request">Restore request.</param>
        /// <returns>Result of restore operation.</returns>
        bool Restore(SolutionRestoreRequest request);

        /// <summary>
        /// Cleans incremental restore cache.
        /// </summary>
        void CleanCache();
    }
}
