// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
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
        private const int SaneIdleTimeoutMs = 400;
        private const int SaneRequestQueueLimit = 150;
        private const int SanePromoteAttemptsLimit = 150;

        private readonly IServiceProvider _serviceProvider;
        private readonly EnvDTE.DTE _dte;
        private readonly IVsSolutionManager _solutionManager;
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private readonly ErrorListProvider _errorListProvider;
        private readonly EnvDTE.SolutionEvents _solutionEvents;

        private CancellationTokenSource _workerCts;
        private Lazy<Task> _backgroundJobRunner;
        private BackgroundRestoreOperation _pendingRestore;
        private BlockingCollection<SolutionRestoreRequest> _pendingRequests;
        private Task<bool> _activeRestoreTask;

        private SolutionRestoreJobContext _restoreJobContext;

        public Task<bool> CurrentRestoreOperation => _activeRestoreTask;

        [ImportingConstructor]
        public SolutionRestoreWorker(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IVsSolutionManager solutionManager,
            IPackageRestoreManager packageRestoreManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            ISettings settings)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            if (packageRestoreManager == null)
            {
                throw new ArgumentNullException(nameof(packageRestoreManager));
            }

            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositoryProvider));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _serviceProvider = serviceProvider;
            _solutionManager = solutionManager;
            _packageRestoreManager = packageRestoreManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;

            _dte = _serviceProvider.GetDTE();
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            _errorListProvider = new ErrorListProvider(_serviceProvider);

            Reset();
        }

        public void Dispose()
        {
            Reset(isDisposing: true);
            _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
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

            _pendingRestore?.Dispose();
            _workerCts?.Dispose();

            if (!isDisposing)
            {
                _workerCts = new CancellationTokenSource();

                _backgroundJobRunner = new Lazy<Task>(
                    valueFactory: () => Task.Run(
                        function: () => StartBackgroundJobRunnerAsync(_workerCts.Token),
                        cancellationToken: _workerCts.Token));

                _pendingRequests = new BlockingCollection<SolutionRestoreRequest>(SaneRequestQueueLimit);
                _pendingRestore = new BackgroundRestoreOperation();
                _activeRestoreTask = Task.FromResult(true);
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

            var pendingRestore = _pendingRestore;

            // on-board request onto pending restore operation
            _pendingRequests.TryAdd(request);

            return pendingRestore.Task;
        }

        public bool Restore(SolutionRestoreRequest request)
        {
            return ThreadHelper.JoinableTaskFactory.Run(
                async () =>
                {
                    // Hop onto a background pool thread
                    await TaskScheduler.Default;

                    using (var restoreOperation = new BackgroundRestoreOperation())
                    {
                        await PromoteTaskToActiveAsync(restoreOperation, _workerCts.Token);

                        var result = await ProcessRestoreRequestAsync(restoreOperation, request, _workerCts.Token);

                        return result;
                    }
                },
                JoinableTaskCreationOptions.LongRunning);
        }

        public void CleanCache()
        {
            Interlocked.Exchange(ref _restoreJobContext, new SolutionRestoreJobContext());
        }

        private async Task StartBackgroundJobRunnerAsync(CancellationToken token)
        {
            // Hops onto a background pool thread
            await TaskScheduler.Default;

            // Loops forever until it's get cancelled
            while (!token.IsCancellationRequested)
            {
                // Grabs a local copy of pending restore operation
                using (var restoreOperation = _pendingRestore)
                {
                    try
                    {
                        // Blocks the execution until first request is scheduled
                        // Monitors the cancelllation token as well.
                        var request = _pendingRequests.Take(token);

                        token.ThrowIfCancellationRequested();

                        // Claims the ownership over the active task
                        // Awaits for currently running restore to complete
                        await PromoteTaskToActiveAsync(restoreOperation, token);

                        token.ThrowIfCancellationRequested();

                        // Drains the queue
                        while (!_pendingRequests.IsCompleted
                            && !token.IsCancellationRequested)
                        {
                            SolutionRestoreRequest discard;
                            if (!_pendingRequests.TryTake(out discard, SaneIdleTimeoutMs, token))
                            {
                                break;
                            }
                        }

                        token.ThrowIfCancellationRequested();

                        // Replaces pending restore operation with a new one
                        Interlocked.CompareExchange(
                            ref _pendingRestore, new BackgroundRestoreOperation(), restoreOperation);

                        token.ThrowIfCancellationRequested();

                        // Runs restore job with scheduled request params
                        await ProcessRestoreRequestAsync(restoreOperation, request, token);

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
        }

        private async Task<bool> ProcessRestoreRequestAsync(
            BackgroundRestoreOperation restoreOperation,
            SolutionRestoreRequest request,
            CancellationToken token)
        {
            var jobTcs = (TaskCompletionSource<bool>)restoreOperation;

            // Start the restore job in a separate task
            // it will switch into main thread.
            var joinableTask = ThreadHelper.JoinableTaskFactory.RunAsync(
                () => StartRestoreJobAsync(request, token));

            await joinableTask.Task
                .ContinueWith(t =>
                {
                    // propagate the restore task status to the *unbound* active task.
                    if (t.IsFaulted)
                    {
                        jobTcs.TrySetException(t.Exception.InnerExceptions);
                    }
                    else if (t.IsCanceled)
                    {
                        jobTcs.TrySetCanceled();
                    }
                    else
                    {
                        // completed successfully
                        jobTcs.TrySetResult(t.Result);
                    }
                });

            return await jobTcs.Task;
        }

        private async Task PromoteTaskToActiveAsync(BackgroundRestoreOperation restoreOperation, CancellationToken token)
        {
            var pendingTask = restoreOperation.Task;

            int attempt = 0;
            for (var retry = true;
                retry && !token.IsCancellationRequested && attempt != SanePromoteAttemptsLimit;
                attempt++)
            {
                // Grab local copy of active task
                var activeTask = _activeRestoreTask;

                // Await until it completes
                var cancelTcs = new TaskCompletionSource<bool>();
                using (var ctr = token.Register(() => cancelTcs.TrySetCanceled()))
                {
                    try
                    {
                        await Task.WhenAny(activeTask, cancelTcs.Task);
                    }
                    catch
                    {
                        // In case active task has failed for some reason
                        // this should not fail this operation.
                        // The intention was to wait for its completion here.
                    }
                }

                // Try replacing active task with the new one.
                // Retry from the beginning if the active task has changed.
                retry = Interlocked.CompareExchange(
                    ref _activeRestoreTask, pendingTask, activeTask) != activeTask;
            }

            if (attempt == SanePromoteAttemptsLimit)
            {
                throw new InvalidOperationException("Failed promoting pending task.");
            }
        }

        private async Task<bool> StartRestoreJobAsync(
            SolutionRestoreRequest jobArgs, CancellationToken token)
        {
            // To be sure, switch to main thread before doing anything on this method
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            using (var logger = new WaitDialogLogger(
                _serviceProvider,
                _errorListProvider))
            using (var job = new SolutionRestoreJob(
                _serviceProvider,
                _packageRestoreManager,
                _solutionManager,
                _sourceRepositoryProvider,
                _settings,
                logger))
            {
                return await job.ExecuteAsync(jobArgs, _restoreJobContext, token);
            }
        }

        private class BackgroundRestoreOperation : IEquatable<BackgroundRestoreOperation>, IDisposable
        {
            private readonly Guid _id = Guid.NewGuid();

            private TaskCompletionSource<bool> JobTcs { get; } = new TaskCompletionSource<bool>();

            public Task<bool> Task => JobTcs.Task;

            public static explicit operator TaskCompletionSource<bool>(BackgroundRestoreOperation restoreOperation) => restoreOperation.JobTcs;

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

            public void Dispose()
            {
                // Inner code block of using clause may throw an unhandled exception.
                // This'd result in leaving the active task in incomplete state.
                // Hence the next restore operation would hang forever.
                // To resolve this issue we try to cancel the unbound task here.
                if (!Task.IsCompleted && !Task.IsCanceled && !Task.IsFaulted)
                {
                    JobTcs.TrySetCanceled();
                }
            }
        }
    }
}
