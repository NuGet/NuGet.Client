// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI.Test
{
    public class NuGetLockServiceTests : IDisposable
    {
        private readonly NuGetLockService _lockService;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private static DispatchThread DispatchThread { get; }
        private static JoinableTaskContextNode JoinableTaskContext { get; }
        private static JoinableTaskFactory JoinableTaskFactory => JoinableTaskContext.Factory;

        static NuGetLockServiceTests()
        {
            // ThreadHelper in VS requires a persistent dispatcher thread.  Because
            // each unit test executes on a new thread, we create our own
            // persistent thread that acts like a UI thread. This will be invoked just
            // once for the module.
            DispatchThread = new DispatchThread();

            DispatchThread.Invoke(() =>
            {
                // Internally this calls ThreadHelper.SetUIThread(), which
                // causes ThreadHelper to remember this thread for the
                // lifetime of the process as the dispatcher thread.
                var serviceProvider = ServiceProvider.GlobalProvider;
            });

            JoinableTaskContext = new JoinableTaskContextNode(
                new JoinableTaskContext(DispatchThread.Thread, DispatchThread.SyncContext));
        }

        public NuGetLockServiceTests()
        {
            _lockService = new NuGetLockService();
            Assert.False(_lockService.IsLockHeld);
        }

        public void Dispose()
        {
            _lockService.Dispose();
            _cts.Dispose();
        }

        [Fact]
        public async Task ExecuteNuGetOperationAsync()
        {
            var isLockHeld = await _lockService.ExecuteNuGetOperationAsync(() =>
            {
                return Task.FromResult(_lockService.IsLockHeld);
            }, _cts.Token);

            Assert.True(isLockHeld);
            Assert.False(_lockService.IsLockHeld);
        }

        [Fact]
        public async Task ExecuteNuGetOperationAsync_Reentrant()
        {
            var isReentrantSuccessful = false;

            await _lockService.ExecuteNuGetOperationAsync(async () =>
            {
                isReentrantSuccessful = await _lockService.ExecuteNuGetOperationAsync(() =>
                {
                    return Task.FromResult(true);
                }, _cts.Token);

                return Task.FromResult(true);
            }, _cts.Token);

            Assert.True(isReentrantSuccessful);
            Assert.False(_lockService.IsLockHeld);
        }

        [Fact]
        public async Task ExecuteNuGetOperationAsync_WhenCanceledBefore_Throws()
        {
            _cts.Cancel();

            await Assert.ThrowsAsync<SemaphoreFullException>(async () => await _lockService.ExecuteNuGetOperationAsync(() =>
            {
                return Task.FromResult(true);
            }, _cts.Token));

            Assert.False(_lockService.IsLockHeld);
        }

        [Fact]
        public async Task ExecuteNuGetOperationAsync_WhenCanceledAfter_Throws()
        {
            await _lockService.ExecuteNuGetOperationAsync(async () =>
            {
                _cts.Cancel();
                await Task.Delay(TimeSpan.FromSeconds(5));

                return Task.FromResult(true);
            }, _cts.Token);

            Assert.False(_lockService.IsLockHeld);
        }

        [Fact]
        public async Task AcquireLockAsync_WithTwoTasksOnMainThread_SerializesAccess()
        {
            var tcs1 = new TaskCompletionSource<bool>();
            var tcs2 = new TaskCompletionSource<bool>();
            var secondTaskAcquiredTheLock = false;

            var jt1 = JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                // awaits global lock acquisition on the main thread (JTF)
                await _lockService.ExecuteNuGetOperationAsync(async () =>
                {
                    using (var resource = new ProtectedResource())
                    {
                        await JoinableTaskFactory.RunAsync(async () =>
                        {
                            // once lock is acquired proceeds with long running task on a pool thread.
                            await TaskScheduler.Default;

                            // signals the second task the lock is acquired
                            tcs1.TrySetResult(true);
                            // waits for the second task get started
                            await tcs2.Task;
                            // dealys for some extra time emulating long operation
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        });
                    }
                }, _cts.Token);
            });

            var jt2 = JoinableTaskFactory.RunAsync(async () =>
            {
                // ensure first task acquired the lock
                await tcs1.Task;

                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var awaiter = _lockService.ExecuteNuGetOperationAsync(() =>
                {
                    using (var resource = new ProtectedResource())
                    {
                        secondTaskAcquiredTheLock = true;
                    }
                    return Task.FromResult(true);

                }, _cts.Token);

                // signal the first task
                tcs2.TrySetResult(true);

                var x = await awaiter;
            });

            await Task.WhenAll(jt1.Task, jt2.Task);

            Assert.True(secondTaskAcquiredTheLock);
        }

        private class ProtectedResource : IDisposable
        {
            private static int Counter = 0;

            public ProtectedResource()
            {
                Assert.Equal(0, Counter);
                Interlocked.Increment(ref Counter);
            }

            public void Dispose()
            {
                Assert.Equal(1, Counter);
                Interlocked.Decrement(ref Counter);
            }
        }
    }
}
