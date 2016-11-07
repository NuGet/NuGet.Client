// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Solution restore job scheduler.
    /// </summary>
    [Export(typeof(ISolutionRestoreWorker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SolutionRestoreWorker : ISolutionRestoreWorker, IVsSolutionEvents, IVsSolutionLoadEvents, IDisposable
    {
        private const int SaneIdleTimeoutMs = 400;
        private const int SaneRequestQueueLimit = 150;
        private const int SanePromoteAttemptsLimit = 150;

        private readonly IServiceProvider _serviceProvider;
        private ErrorListProvider _errorListProvider;
        private EnvDTE.SolutionEvents _solutionEvents;
        private readonly IComponentModel _componentModel;
        private readonly IVsSolutionManager _solutionManager;

        // these properties are specific to VS15 since they are use to attach to solution events
        // which is further used to start bg job runner to schedule auto restore
#if VS15
        private IVsSolution _vsSolution;
        private uint _cookie;
#endif
        private CancellationTokenSource _workerCts;
        private Lazy<Task> _backgroundJobRunner;
        private BackgroundRestoreOperation _pendingRestore;
        private BlockingCollection<SolutionRestoreRequest> _pendingRequests;
        private Task<bool> _activeRestoreTask;

        private SolutionRestoreJobContext _restoreJobContext;

        private readonly JoinableTaskCollection _joinableCollection;
        private readonly JoinableTaskFactory _joinableFactory;

        public Task<bool> CurrentRestoreOperation => _activeRestoreTask;

        public bool IsBusy => !_activeRestoreTask.IsCompleted;

        [ImportingConstructor]
        public SolutionRestoreWorker(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IVsSolutionManager solutionManager)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            _serviceProvider = serviceProvider;
            _solutionManager = solutionManager;

            var joinableTaskContextNode = new JoinableTaskContextNode(ThreadHelper.JoinableTaskContext);
            _joinableCollection = joinableTaskContextNode.CreateCollection();
            _joinableFactory = joinableTaskContextNode.CreateFactory(_joinableCollection);

            _componentModel = _serviceProvider.GetService<SComponentModel, IComponentModel>();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = _serviceProvider.GetDTE();
                _solutionEvents = dte.Events.SolutionEvents;
                _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

                _errorListProvider = new ErrorListProvider(_serviceProvider);
#if VS15
                _vsSolution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                if (_vsSolution == null)
                {
                    throw new ArgumentNullException(nameof(_vsSolution));
                }

                if (_vsSolution.AdviseSolutionEvents(this, out _cookie) == VSConstants.S_OK)
                {
                    Debug.Assert(_cookie != 0);
                }
                else
                {
                    _cookie = 0;
                }
#endif
            });

            Reset();
        }

        public void Dispose()
        {
            Reset(isDisposing: true);

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
                _errorListProvider.Dispose();
#if VS15
                if (_cookie != 0 && _vsSolution != null)
                {
                    _vsSolution.UnadviseSolutionEvents(_cookie);
                }
#endif
            });
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
                _pendingRestore = new BackgroundRestoreOperation(blockingUi: false);
                _activeRestoreTask = Task.FromResult(true);
                _restoreJobContext = new SolutionRestoreJobContext();
            }
        }

        private void SolutionEvents_AfterClosing()
        {
            Reset();
            _errorListProvider.Tasks.Clear();
        }

        public async Task<bool> ScheduleRestoreAsync(
            SolutionRestoreRequest request, CancellationToken token)
        {
            if (_solutionManager.IsSolutionFullyLoaded)
            {
                // start background runner if not yet started
                // ignore the value
                var runner = _backgroundJobRunner.Value;
            }

            var pendingRestore = _pendingRestore;

            // on-board request onto pending restore operation
            _pendingRequests.TryAdd(request);

            using (_joinableCollection.Join())
            {
                return await (Task<bool>)pendingRestore;
            }
        }

        public bool Restore(SolutionRestoreRequest request)
        {
            return ThreadHelper.JoinableTaskFactory.Run(
                async () =>
                {
                    using (var restoreOperation = new BackgroundRestoreOperation(blockingUi: true))
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

                        // Replaces pending restore operation with a new one.
                        // Older value is ignored.
                        var ignore = Interlocked.CompareExchange(
                            ref _pendingRestore, new BackgroundRestoreOperation(blockingUi: false), restoreOperation);

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
            // Start the restore job in a separate task on a background thread
            // it will switch into main thread when necessary.
            var joinableTask = _joinableFactory.RunAsync(
                () => StartRestoreJobAsync(request, restoreOperation.BlockingUI, token));

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
                retry && !token.IsCancellationRequested && attempt != SanePromoteAttemptsLimit;
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

            if (attempt == SanePromoteAttemptsLimit)
            {
                throw new InvalidOperationException("Failed promoting pending task.");
            }
        }

        private async Task<bool> StartRestoreJobAsync(
            SolutionRestoreRequest jobArgs, bool blockingUi, CancellationToken token)
        {
            await TaskScheduler.Default;

            using (var jobCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var logger = await RestoreOperationLogger.StartAsync(
                _serviceProvider, _errorListProvider, blockingUi, jobCts))
            using (var job = await SolutionRestoreJob.CreateAsync(
                _serviceProvider, _componentModel, logger, jobCts.Token))
            {
                return await job.ExecuteAsync(jobArgs, _restoreJobContext, jobCts.Token);
            }
        }

#region IVsSolutionEvents (mandatory but unused implementation)
        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }
#endregion

#region IVsSolutionLoadEvents (Only useful implementation is OnAfterBackgroundSolutionLoadComplete)
        public int OnBeforeOpenSolution(string pszSolutionFilename)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeBackgroundSolutionLoadBegins()
        {
            return VSConstants.S_OK;
        }

        public int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle)
        {
            pfShouldDelayLoadToNextIdle = false;
            return VSConstants.S_OK;
        }

        public int OnBeforeLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterBackgroundSolutionLoadComplete()
        {
#if VS15
            var projects = _solutionManager.GetNuGetProjects();
            if (projects.Any(project => (project is CpsPackageReferenceProject)))
            {
                // ensure background runner has started
                // ignore the value
                var runner = _backgroundJobRunner.Value;
            }
#endif
            return VSConstants.S_OK;
        }
#endregion

        private class BackgroundRestoreOperation
            : IEquatable<BackgroundRestoreOperation>, IDisposable
        {
            private readonly Guid _id = Guid.NewGuid();

            private TaskCompletionSource<bool> JobTcs { get; } = new TaskCompletionSource<bool>();

            private Task<bool> Task => JobTcs.Task;

            public System.Runtime.CompilerServices.TaskAwaiter<bool> GetAwaiter() => Task.GetAwaiter();

            public static explicit operator Task<bool>(BackgroundRestoreOperation restoreOperation) => restoreOperation.Task;

            public bool BlockingUI { get; }

            public BackgroundRestoreOperation(bool blockingUi)
            {
                BlockingUI = blockingUi;
            }

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
