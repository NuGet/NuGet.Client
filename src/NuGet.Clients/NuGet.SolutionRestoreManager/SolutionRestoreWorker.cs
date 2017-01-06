// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Solution restore job scheduler.
    /// </summary>
    [Export(typeof(ISolutionRestoreWorker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SolutionRestoreWorker : SolutionEventsListener, ISolutionRestoreWorker, IDisposable
    {
        private const int IdleTimeoutMs = 400;
        private const int RequestQueueLimit = 150;
        private const int PromoteAttemptsLimit = 150;

        private readonly IServiceProvider _serviceProvider;
        private readonly AsyncLazy<ErrorListProvider> _errorListProvider;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<INuGetLockService> _lockService;
        private readonly Lazy<Common.ILogger> _logger;
        private readonly AsyncLazy<IComponentModel> _componentModel;

        private EnvDTE.SolutionEvents _solutionEvents;
        private CancellationTokenSource _workerCts;
        private Lazy<Task> _backgroundJobRunner;
        private Lazy<BlockingCollection<SolutionRestoreRequest>> _pendingRequests;
        private BackgroundRestoreOperation _pendingRestore;
        private Task<bool> _activeRestoreTask;
        private int _initialized;

        private SolutionRestoreJobContext _restoreJobContext;

        private readonly JoinableTaskCollection _joinableCollection;
        private readonly JoinableTaskFactory _joinableFactory;

        private ErrorListProvider ErrorListProvider => ThreadHelper.JoinableTaskFactory.Run(_errorListProvider.GetValueAsync);

        public Task<bool> CurrentRestoreOperation => _activeRestoreTask;

        public bool IsBusy => !_activeRestoreTask.IsCompleted;

        [ImportingConstructor]
        public SolutionRestoreWorker(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            Lazy<IVsSolutionManager> solutionManager,
            Lazy<INuGetLockService> lockService,
            [Import(typeof(VisualStudioActivityLogger))]
            Lazy<Common.ILogger> logger)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            if (lockService == null)
            {
                throw new ArgumentNullException(nameof(lockService));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _serviceProvider = serviceProvider;
            _solutionManager = solutionManager;
            _lockService = lockService;
            _logger = logger;

            var joinableTaskContextNode = new JoinableTaskContextNode(ThreadHelper.JoinableTaskContext);
            _joinableCollection = joinableTaskContextNode.CreateCollection();
            _joinableFactory = joinableTaskContextNode.CreateFactory(_joinableCollection);

            _errorListProvider = new AsyncLazy<ErrorListProvider>(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return new ErrorListProvider(serviceProvider);
                },
                _joinableFactory);

            _componentModel = new AsyncLazy<IComponentModel>(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return serviceProvider.GetService<SComponentModel, IComponentModel>();
                },
                _joinableFactory);

            Reset();
        }

        private async Task InitializeAsync()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                await _joinableFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var dte = _serviceProvider.GetDTE();
                    _solutionEvents = dte.Events.SolutionEvents;
                    _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
#if VS15
                    // these properties are specific to VS15 since they are use to attach to solution events
                    // which is further used to start bg job runner to schedule auto restore
                    Advise(_serviceProvider);
#endif
                });
            }
        }

        public void Dispose()
        {
            Reset(isDisposing: true);

            _joinableFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
#if VS15
                Unadvise();
#endif
                if (_errorListProvider.IsValueCreated)
                {
                    (await _errorListProvider.GetValueAsync()).Dispose();
                }
            });
        }

        private void Reset(bool isDisposing = false)
        {
            _workerCts?.Cancel();

            if (_backgroundJobRunner?.IsValueCreated == true)
            {
                // Do not block VS for more than 5 sec.
                _joinableFactory.Run(
                    () => Task.WhenAny(_backgroundJobRunner.Value, Task.Delay(TimeSpan.FromSeconds(5))));
            }

            _pendingRestore?.Dispose();
            _workerCts?.Dispose();

            if (_pendingRequests?.IsValueCreated == true)
            {
                _pendingRequests.Value.Dispose();
            }

            if (!isDisposing)
            {
                _workerCts = new CancellationTokenSource();

                _backgroundJobRunner = new Lazy<Task>(
                    valueFactory: () => Task.Run(
                        function: () => StartBackgroundJobRunnerAsync(_workerCts.Token),
                        cancellationToken: _workerCts.Token));

                _pendingRequests = new Lazy<BlockingCollection<SolutionRestoreRequest>>(
                    () => new BlockingCollection<SolutionRestoreRequest>(RequestQueueLimit));

                _pendingRestore = new BackgroundRestoreOperation();
                _activeRestoreTask = Task.FromResult(true);
                _restoreJobContext = new SolutionRestoreJobContext();
            }
        }

        private void SolutionEvents_AfterClosing()
        {
            Reset();
            ErrorListProvider.Tasks.Clear();
        }

        public async Task<bool> ScheduleRestoreAsync(
            SolutionRestoreRequest request, CancellationToken token)
        {
            // Initialize if not already done.
            await InitializeAsync();

            if (_solutionManager.Value.IsSolutionFullyLoaded)
            {
                // start background runner if not yet started
                // ignore the value
                var ignore = _backgroundJobRunner.Value;
            }

            var pendingRestore = _pendingRestore;

            // on-board request onto pending restore operation
            _pendingRequests.Value.TryAdd(request);

            using (_joinableCollection.Join())
            {
                return await (Task<bool>)pendingRestore;
            }
        }

        public bool Restore(SolutionRestoreRequest request)
        {
            return _joinableFactory.Run(
                async () =>
                {
                    // Initialize if not already done.
                    await InitializeAsync();
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
                        var request = _pendingRequests.Value.Take(token);

                        token.ThrowIfCancellationRequested();

                        // Claims the ownership over the active task
                        // Awaits for currently running restore to complete
                        await PromoteTaskToActiveAsync(restoreOperation, token);

                        token.ThrowIfCancellationRequested();

                        // Drains the queue
                        while (!_pendingRequests.Value.IsCompleted
                            && !token.IsCancellationRequested)
                        {
                            SolutionRestoreRequest discard;
                            if (!_pendingRequests.Value.TryTake(out discard, IdleTimeoutMs, token))
                            {
                                break;
                            }
                        }

                        token.ThrowIfCancellationRequested();

                        // Replaces pending restore operation with a new one.
                        // Older value is ignored.
                        var ignore = Interlocked.CompareExchange(
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
                    catch (Exception e)
                    {
                        // Writes stack to activity log
                        _logger.Value.LogError(e.ToString());
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
            // Start the restore job in a separate task on a background thread
            // it will switch into main thread when necessary.
            var joinableTask = _joinableFactory.RunAsync(
                () => StartRestoreJobAsync(request, token));

            var continuation = joinableTask
                .Task
                .ContinueWith(t => restoreOperation.ContinuationAction(t));

            return await joinableTask;
        }

        private async Task PromoteTaskToActiveAsync(BackgroundRestoreOperation restoreOperation, CancellationToken token)
        {
            var pendingTask = (Task<bool>)restoreOperation;

            int attempt = 0;
            for (var retry = true;
                retry && !token.IsCancellationRequested && attempt != PromoteAttemptsLimit;
                attempt++)
            {
                // Grab local copy of active task
                var activeTask = _activeRestoreTask;

                // Await for the completion of the active *unbound* task
                var cancelTcs = new TaskCompletionSource<bool>();
                using (var ctr = token.Register(() => cancelTcs.TrySetCanceled()))
                {
                    await Task.WhenAny(activeTask, cancelTcs.Task);
                }

                // Try replacing active task with the new one.
                // Retry from the beginning if the active task has changed.
                retry = Interlocked.CompareExchange(
                    ref _activeRestoreTask, pendingTask, activeTask) != activeTask;
            }

            if (attempt == PromoteAttemptsLimit)
            {
                throw new InvalidOperationException("Failed promoting pending task.");
            }
        }

        private async Task<bool> StartRestoreJobAsync(
            SolutionRestoreRequest request, CancellationToken token)
        {
            await TaskScheduler.Default;

            using (var jobCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var lck = await _lockService.Value.AcquireLockAsync(jobCts.Token))
            {
                var componentModel = await _componentModel.GetValueAsync(jobCts.Token);

                var logger = componentModel.GetService<RestoreOperationLogger>();
                await logger.StartAsync(
                    request.RestoreSource,
                    ErrorListProvider,
                    jobCts);

                var job = componentModel.GetService<ISolutionRestoreJob>();
                return await job.ExecuteAsync(request, _restoreJobContext, logger, jobCts.Token);
            }
        }

        public override int OnAfterBackgroundSolutionLoadComplete()
        {
            if (_pendingRequests.IsValueCreated)
            {
                // ensure background runner has started
                // ignore the value
                var ignore = _backgroundJobRunner.Value;
            }

            return VSConstants.S_OK;
        }

        private class BackgroundRestoreOperation
            : IEquatable<BackgroundRestoreOperation>, IDisposable
        {
            private readonly Guid _id = Guid.NewGuid();

            private TaskCompletionSource<bool> JobTcs { get; } = new TaskCompletionSource<bool>();

            private Task<bool> Task => JobTcs.Task;

            public System.Runtime.CompilerServices.TaskAwaiter<bool> GetAwaiter() => Task.GetAwaiter();

            public static explicit operator Task<bool>(BackgroundRestoreOperation restoreOperation) => restoreOperation.Task;

            public void ContinuationAction(Task<bool> targetTask)
            {
                // propagate the restore target task status to the *unbound* active task.
                if (targetTask.IsFaulted || targetTask.IsCanceled)
                {
                    // fail the restore result if the target task has failed or cancelled.
                    JobTcs.TrySetResult(result: false);
                }
                else
                {
                    // completed successfully
                    JobTcs.TrySetResult(targetTask.Result);
                }
            }

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
                // To resolve potential deadlock issue the unbound task is to be completed here.
                if (!Task.IsCompleted && !Task.IsCanceled && !Task.IsFaulted)
                {
                    JobTcs.TrySetResult(result: false);
                }
            }
        }
    }
}
