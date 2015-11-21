// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Session wide URI lock
    /// </summary>
    public class UriLock : IDisposable
    {
        // session wide locks
        private static readonly ConcurrentDictionary<Uri, Guid> _locks = new ConcurrentDictionary<Uri, Guid>();
        private readonly Uri _uri;
        private readonly int _msWait;
        private readonly Guid _guid;
        private readonly CancellationToken _cancellationToken;

        public UriLock(Uri uri, CancellationToken cancellationToken, int msWait = 100)
        {
            _uri = uri;
            _msWait = msWait;
            _guid = Guid.NewGuid();
            _cancellationToken = cancellationToken;

            GetLock();
        }

        private void GetLock()
        {
            while (!_cancellationToken.IsCancellationRequested
                   && !_locks.TryAdd(_uri, _guid))
            {
                // spin lock
                Thread.Sleep(_msWait);
            }
        }

        private void ReleaseLock()
        {
            Guid obj;
            if (!_locks.TryRemove(_uri, out obj))
            {
                Debug.Fail("Missing lock object!");
            }
        }

        public void Dispose()
        {
            ReleaseLock();
        }
    }
}
