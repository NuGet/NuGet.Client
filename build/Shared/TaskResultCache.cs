// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet
{
    /// <summary>
    /// Provides a caching mechanism for async operations.
    /// </summary>
    /// <typeparam name="TKey">The key to use for storing the async operation.</typeparam>
    /// <typeparam name="TValue">The return type of the async operation.</typeparam>
    internal sealed class TaskResultCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, Task<TValue>> _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResultCache{TKey, TValue}" /> class with the specified key comparer.
        /// </summary>
        /// <param name="comparer">An <see cref="IEqualityComparer{T}" to use when comparing keys.</param>
        public TaskResultCache(IEqualityComparer<TKey> comparer)
        {
            _cache = new(comparer);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResultCache{TKey, TValue}" /> class.
        /// </summary>
        public TaskResultCache()
        {
            _cache = new();
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> valueFactory)
        {
            return GetOrAddAsync(key, refresh: false, valueFactory, CancellationToken.None);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to use for signaling that an operation should be cancelled.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> valueFactory, CancellationToken cancellationToken)
        {
            return GetOrAddAsync(key, refresh: false, valueFactory, cancellationToken);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await, and optionally refreshes the cache.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="refresh"><see langword="true" /> to force the specified asynchronous operation to be executed and stored in the cache even if a cached operation exists, otherwise <see langword="false" />.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync(TKey key, bool refresh, Func<Task<TValue>> valueFactory)
        {
            return GetOrAddAsync(key, refresh, valueFactory, CancellationToken.None);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await, and optionally refreshes the cache.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="refresh"><see langword="true" /> to force the specified asynchronous operation to be executed and stored in the cache even if a cached operation exists, otherwise <see langword="false" />.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to use for signaling that an operation should be cancelled.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync(TKey key, bool refresh, Func<Task<TValue>> valueFactory, CancellationToken cancellationToken)
        {
            if (!refresh && _cache.TryGetValue(key, out Task<TValue>? result))
            {
                return result;
            }

            lock (_cache)
            {
                if (!refresh && _cache.TryGetValue(key, out result))
                {
                    return result;
                }

                result = valueFactory()
                    .ContinueWith(
                        (task, state) => task.GetAwaiter().GetResult(),
                        state: null,
                        cancellationToken,
                        TaskContinuationOptions.RunContinuationsAsynchronously,
                        TaskScheduler.Default);

                _cache[key] = result;

                return result;
            }
        }
    }
}
