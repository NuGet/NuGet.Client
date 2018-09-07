// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// MEF component providing the lock which guarantees non-overlapping execution of NuGet operations.
    /// </summary>
    [Export(typeof(INuGetLockService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class NuGetLockService : INuGetLockService, IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public bool IsLockHeld => _semaphore.CurrentCount == 0;

        public IDisposable AcquireLock()
        {
            _semaphore.Wait();
            return new SemaphoreLockReleaser(_semaphore);
        }

        public IAsyncLockAwaitable AcquireLockAsync(CancellationToken token)
        {
            return new SemaphoreLockAwaiter(_semaphore, token);
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        private class SemaphoreLockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _isDisposed = false;

            public SemaphoreLockReleaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool _)
            {
                if (_isDisposed)
                {
                    return;
                }

                try
                {
                    _semaphore.Release();
                }
                catch (ObjectDisposedException) { }

                _isDisposed = true;
            }

            ~SemaphoreLockReleaser()
            {
                Dispose(false);
            }
        }

        // Custom awaiter which wraps the awaiter for a task awaiting the semaphore lock.
        // This awaiter implementation wraps a TaskAwaiter, and this implementation’s 
        // IsCompleted, OnCompleted, and GetResult members delegate to the contained TaskAwaiter’s.
        private class SemaphoreLockAwaiter : AsyncLockAwaiter, IAsyncLockAwaitable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly CancellationToken _token;
            private readonly TaskAwaiter _awaiter;

            private bool _isReleased = false;

            public SemaphoreLockAwaiter(SemaphoreSlim semaphore, CancellationToken token)
            {
                if (semaphore == null)
                {
                    throw new ArgumentNullException(nameof(semaphore));
                }

                if (token == null)
                {
                    throw new ArgumentNullException(nameof(token));
                }

                _semaphore = semaphore;
                _token = token;
                _awaiter = _semaphore.WaitAsync(token).GetAwaiter();
            }

            public AsyncLockAwaiter GetAwaiter() => this;

            public override bool IsCompleted => _awaiter.IsCompleted;

            public override IDisposable GetResult()
            {
                try
                {
                    _awaiter.GetResult();
                }
                catch (TaskCanceledException) when (_token.IsCancellationRequested)
                {
                    // when token is canceled before WaitAsync is called 
                    // GetResult would throw TaskCanceledException.
                    // This clause solves this incosistency.
                    throw new OperationCanceledException();
                }

                return new AsyncLockReleaser(this);
            }

            public override void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

            public override void Release()
            {
                if (!_isReleased)
                {
                    _isReleased = true;

                    try
                    {
                        _semaphore.Release();
                    }
                    catch (ObjectDisposedException) { }
                }
            }
        }
    }
}
