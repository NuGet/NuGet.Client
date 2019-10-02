// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    internal class InProcLock : IDisposable
    {
        private Dictionary<string, LockState> _locks;
        private SemaphoreSlim _dictionaryLock;

        internal InProcLock()
        {
            _locks = new Dictionary<string, LockState>();
            _dictionaryLock = new SemaphoreSlim(1);
        }

        internal async Task EnterAsync(string key, CancellationToken token)
        {
            LockState lockState;

            await _dictionaryLock.WaitAsync(token);
            try
            {
                lockState = GetOrCreate(key);
            }
            finally
            {
                _dictionaryLock.Release();
            }

            try
            {
                await lockState.Semaphore.WaitAsync(token);
            }
            catch
            {
                Interlocked.Decrement(ref lockState.Count);
            }
        }

        internal void Enter(string key, CancellationToken token)
        {
            LockState lockState;

            _dictionaryLock.Wait(token);
            try
            {
                lockState = GetOrCreate(key);
            }
            finally
            {
                _dictionaryLock.Release();
            }

            try
            {
                lockState.Semaphore.Wait(token);
            }
            catch
            {
                Interlocked.Decrement(ref lockState.Count);
            }
        }

        private LockState GetOrCreate(string key)
        {
            if (!_locks.TryGetValue(key, out var lockState))
            {
                lockState = new LockState();
                lockState.Semaphore = new SemaphoreSlim(1);
                lockState.Count = 1;
                _locks[key] = lockState;
            }
            else
            {
                Interlocked.Increment(ref lockState.Count);
            }

            return lockState;
        }

        internal async Task ExitAsync(string key)
        {
            await _dictionaryLock.WaitAsync();
            try
            {
                Cleanup(key);
            }
            finally
            {
                _dictionaryLock.Release();
            }
        }

        internal void Exit(string key)
        {
            _dictionaryLock.Wait();
            try
            {
                Cleanup(key);
            }
            finally
            {
                _dictionaryLock.Release();
            }
        }

        private void Cleanup(string key)
        {
            var lockState = _locks[key];
            lockState.Semaphore.Release();
            var count = Interlocked.Decrement(ref lockState.Count);
            if (count == 0)
            {
                lockState.Semaphore.Release();
                _locks.Remove(key);
            }
        }

        private class LockState
        {
            public SemaphoreSlim Semaphore;
            public int Count;
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _dictionaryLock.Dispose();
                    _locks.Clear();
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
