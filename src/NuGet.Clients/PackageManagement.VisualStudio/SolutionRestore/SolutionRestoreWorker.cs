// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Solution restore job scheduler.
    /// </summary>
    [Export(typeof(ISolutionRestoreWorker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SolutionRestoreWorker : ISolutionRestoreWorker, IDisposable
    {
        private const int IdleTimeoutMs = 400;
        private const int RequestQueueLimit = 150;
        private const int PromoteAttemptsLimit = 150;

        private readonly IServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private readonly EnvDTE.SolutionEvents _solutionEvents;
        private readonly IComponentModel _componentModel;

        private CancellationTokenSource _workerCts;
        private Lazy<Task> _backgroundJobRunner;
        private BackgroundRestoreOperation _activeRestoreOperation;
        private BlockingCollection<SolutionRestoreRequest> _pendingRequests;
        private readonly SemaphoreSlim _activeLock = new SemaphoreSlim(1, 1);

        private SolutionRestoreJobContext _restoreJobContext;

        public Task<bool> CurrentRestoreOperation => _activeRestoreOperation.JobTask;

        public bool IsBusy => !_activeRestoreOperation.JobTask.IsCompleted;

        [ImportingConstructor]
        public SolutionRestoreWorker(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;

            _componentModel = _serviceProvider.GetService<SComponentModel, IComponentModel>();

            var dte = _serviceProvider.GetDTE();
            _solutionEvents = dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            _errorListProvider = new ErrorListProvider(_serviceProvider);

            Reset();
        }

        public void Dispose()
        {
            _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;

            Reset(isDisposing: true);
            _errorListProvider.Dispose();
        }

        private void Reset(bool isDisposing = false)
        {
            _workerCts?.Cancel();

            if (_backgroundJobRunner != null && _backgroundJobRunner.IsValueCreated)
            {
                // Do not block VS for more than 5 sec.
                ThreadHelper.JoinableTaskFactory.Run(
                    () => Task.WhenAny(_backgroundJobRunner.Value, Task.Delay(TimeSpan.FromSeconds(5))));
            }

            _workerCts?.Dispose();

            if (!isDisposing)
            {
                _workerCts = new CancellationTokenSource();

                _backgroundJobRunner = new Lazy<Task>(
                    valueFactory: () => Task.Run(
                        function: () => StartBackgroundJobRunnerAsync(_workerCts.Token),
                        cancellationToken: _workerCts.Token));

                _pendingRequests = new BlockingCollection<SolutionRestoreRequest>(RequestQueueLimit);
                _activeRestoreOperation = BackgroundRestoreOperation.Default;
                _restoreJobContext = new SolutionRestoreJobContext();
            }
        }

        private void SolutionEvents_AfterClosing()
        {
            Reset();
            _errorListProvider.Tasks.Clear();
        }

        public Task<bool> ScheduleRestoreAsync(
            SolutionRestoreRequest request, CancellationToken token)
        {
            // ensure background runner has started
            // ignore the value
            var runner = _backgroundJobRunner.Value;
            Trace.TraceInformation($"Scheduling background solution restore. The background runner's status is '{runner.Status}'");

            // on-board request onto pending restore operation
            _pendingRequests.TryAdd(request);

            return Task.FromResult(true);
        }

        public bool Restore(SolutionRestoreRequest request)
        {
            var restoreOperation = ProcessRestoreRequestAsync(request, true, _workerCts.Token);
            return restoreOperation.Join(_workerCts.Token);
        }

        public void CleanCache()
        {
            Interlocked.Exchange(ref _restoreJobContext, new SolutionRestoreJobContext());
        }

        private async Task StartBackgroundJobRunnerAsync(CancellationToken token)
        {
            // Loops forever until it's get cancelled
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Blocks the execution until first request is scheduled
                    // Monitors the cancelllation token as well.
                    var request = _pendingRequests.Take(token);

                    token.ThrowIfCancellationRequested();

                    // Consumes all pending requests just before the start
                    DrainPendingRequestQueue(token);

                    token.ThrowIfCancellationRequested();

                    // Runs restore job with scheduled request params
                    var restoreOperation = ProcessRestoreRequestAsync(request, false, token);
                    // Awaits the job task
                    await restoreOperation.JobTask;

                    // Repeats...
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Ignores
                }
                catch (Exception ex)
                {
                    // Writes stack to activity log
                    ExceptionHelper.WriteToActivityLog(ex);
                    // Do not die just yet
                }
            }
        }

        private BackgroundRestoreOperation ProcessRestoreRequestAsync(
            SolutionRestoreRequest request,
            bool blockingUi,
            CancellationToken token)
        {
            _activeLock.Wait(token);

            BackgroundRestoreOperation newRestoreOperation;
            try
            {
                var activeRestoreOperation = _activeRestoreOperation;

                newRestoreOperation = BackgroundRestoreOperation.Start(
                    () => StartRestoreJobAsync(request, blockingUi, activeRestoreOperation, token));

                _activeRestoreOperation = newRestoreOperation;
            }
            finally
            {
                _activeLock.Release();
            }

            return newRestoreOperation;
        }

        private async Task<bool> StartRestoreJobAsync(
            SolutionRestoreRequest request,
            bool blockingUi,
            BackgroundRestoreOperation activeRestoreOperation,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            await activeRestoreOperation;

            token.ThrowIfCancellationRequested();

            DrainPendingRequestQueue(token);

            token.ThrowIfCancellationRequested();

            using (var jobCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var logger = await RestoreOperationLogger.StartAsync(
                _serviceProvider, _errorListProvider, blockingUi, jobCts))
            using (var job = await SolutionRestoreJob.CreateAsync(
                _serviceProvider, _componentModel, logger, jobCts.Token))
            {
                return await job.ExecuteAsync(request, _restoreJobContext, jobCts.Token);
            }
        }

        private void DrainPendingRequestQueue(CancellationToken token)
        {
            while (!_pendingRequests.IsCompleted
                && !token.IsCancellationRequested)
            {
                SolutionRestoreRequest discard;
                if (!_pendingRequests.TryTake(out discard, IdleTimeoutMs, token))
                {
                    break;
                }
            }
        }

        private class BackgroundRestoreOperation : IEquatable<BackgroundRestoreOperation>
        {
            private readonly Guid _id = Guid.NewGuid();

            // joinable task needed for chaining operations
            private readonly JoinableTask<bool> _joinableTask;

            // *unbound* task needed for signaling external consumers
            public Task<bool> JobTask { get; }

            public System.Runtime.CompilerServices.TaskAwaiter<bool> GetAwaiter()
                => _joinableTask.GetAwaiter();

            public static explicit operator JoinableTask<bool>(BackgroundRestoreOperation restoreOperation)
                => restoreOperation._joinableTask;

            public static BackgroundRestoreOperation Default { get; } = new BackgroundRestoreOperation();

            private BackgroundRestoreOperation()
            {
                _joinableTask = ThreadHelper.JoinableTaskFactory.RunAsync(
                    () => Task.FromResult(true));

                JobTask = Task.FromResult(true);
            }

            private BackgroundRestoreOperation(
                JoinableTask<bool> joinableTask)
            {
                _joinableTask = joinableTask;

                var jobTcs = new TaskCompletionSource<bool>();
                joinableTask
                    .Task
                    .ContinueWith(t => ContinuationAction(t, jobTcs));

                JobTask = jobTcs.Task;
            }

            public static BackgroundRestoreOperation Start(Func<Task<bool>> jobRoutine)
            {
                // Start the restore operation in a joinable task on a background thread
                // it will switch into main thread when necessary.
                var joinableTask = ThreadHelper.JoinableTaskFactory.RunAsync(jobRoutine);

                return new BackgroundRestoreOperation(joinableTask);
            }

            private static void ContinuationAction(Task<bool> targetTask, TaskCompletionSource<bool> jobTcs)
            {
                // propagate the restore target task status to the *unbound* active task.
                if (targetTask.IsFaulted || targetTask.IsCanceled)
                {
                    // fail the restore result if the target task has failed or cancelled.
                    jobTcs.TrySetResult(result: false);
                }
                else
                {
                    // completed successfully
                    jobTcs.TrySetResult(targetTask.Result);
                }
            }

            public bool Join(CancellationToken token) => _joinableTask.Join(token);

            public bool Equals(BackgroundRestoreOperation other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return _id == other._id;
            }

            public override bool Equals(object obj) => Equals(obj as BackgroundRestoreOperation);

            public override int GetHashCode() => _id.GetHashCode();

            public static bool operator ==(BackgroundRestoreOperation left, BackgroundRestoreOperation right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(BackgroundRestoreOperation left, BackgroundRestoreOperation right)
            {
                return !Equals(left, right);
            }

            public override string ToString() => _id.ToString();
        }
    }
}
