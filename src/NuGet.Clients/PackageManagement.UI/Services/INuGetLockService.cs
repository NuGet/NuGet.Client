// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Defines a contract for a service providing a locking mechanism which guarantees non-overlapping execution of NuGet operations.
    /// </summary>
    public interface INuGetLockService
    {
        /// <summary>
        /// Gets a value indicating whether any kind of lock is held.
        /// </summary>
        bool IsLockHeld { get; }

        /// <summary>
        /// Obtains a lock, asynchronously awaiting for the lock if it is not immediately awailable.
        /// </summary>
        /// <param name="token">A token whose cancellation indicates lost interest in obtaining the lock.</param>
        /// <returns>An awaitable object whose result is the lock releaser.</returns>
        IAsyncLockAwaitable AcquireLockAsync(CancellationToken token);

        /// <summary>
        /// Obtains a lock in synchronous/blocking manner.
        /// </summary>
        /// <returns>A disposable object that will release the lock on disposed event.</returns>
        IDisposable AcquireLock();
    }
}
