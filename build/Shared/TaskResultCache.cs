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
        public Task<TValue> GetOrAddAsync<TState>(TKey key, Func<TState, Task<TValue>> valueFactory, TState factoryState)
        {
            return GetOrAddAsync(key, refresh: false, valueFactory, factoryState, CancellationToken.None);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to use for signaling that an operation should be cancelled.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync<TState>(TKey key, Func<TState, Task<TValue>> valueFactory, TState factoryState, CancellationToken cancellationToken)
        {
            return GetOrAddAsync(key, refresh: false, valueFactory, factoryState, cancellationToken);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await, and optionally refreshes the cache.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="refresh"><see langword="true" /> to force the specified asynchronous operation to be executed and stored in the cache even if a cached operation exists, otherwise <see langword="false" />.</param>
        /// <param name="valueFactory">A <see cref="Func{TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync<TState>(TKey key, bool refresh, Func<TState, Task<TValue>> valueFactory, TState factoryState)
        {
            return GetOrAddAsync(key, refresh, valueFactory, factoryState, CancellationToken.None);
        }

        /// <summary>
        /// Gets the cached async operation associated with the specified key, or runs the operation asynchronously and returns <see cref="Task{TValue}" /> that the caller can await, and optionally refreshes the cache.
        /// </summary>
        /// <param name="key">The key for the async operation to get or store in the cache.</param>
        /// <param name="refresh"><see langword="true" /> to force the specified asynchronous operation to be executed and stored in the cache even if a cached operation exists, otherwise <see langword="false" />.</param>
        /// <param name="valueFactory">A <see cref="Func{T1, TResult}" /> to execute asynchronously if a cached operation does not exist.</param>
        /// <param name="factoryState">A state object to pass to the value factory.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to use for signaling that an operation should be cancelled.</param>
        /// <returns>A <see cref="Task{TResult}" /> for the specified asynchronous operation from the cache if found, otherwise the scheduled asynchronous operation to await.</returns>
        public Task<TValue> GetOrAddAsync<TState>(TKey key, bool refresh, Func<TState, Task<TValue>> valueFactory, TState factoryState, CancellationToken cancellationToken)
        {
            if (refresh)
            {
                var newTask = StartNewFactoryTask(valueFactory, factoryState, cancellationToken);
                _cache[key] = newTask;
                return newTask;
            }

            if (_cache.TryGetValue(key, out Task<TValue>? value))
            {
                return value;
            }

            return GetValueSlow(key, valueFactory, factoryState, cancellationToken);

            static Task<TValue> StartNewFactoryTask(Func<TState, Task<TValue>> valueFactory, TState factoryState, CancellationToken cancellationToken)
            {
                return Task.Run(() => valueFactory(factoryState), cancellationToken)

                    .ContinueWith(

                        static (task, state) => task.GetAwaiter().GetResult(),

                        state: null,

                        cancellationToken,

                        TaskContinuationOptions.RunContinuationsAsynchronously,

                        TaskScheduler.Default);
            }

            Task<TValue> GetValueSlow(TKey key, Func<TState, Task<TValue>> valueFactory, TState factoryState, CancellationToken cancellationToken)
            {
                lock (_cache)
                {
                    if (_cache.TryGetValue(key, out Task<TValue>? value))
                    {
                        return value;
                    }

                    value = StartNewFactoryTask(valueFactory, factoryState, cancellationToken);

                    _cache[key] = value;

                    return value;
                }
            }
        }
    }
}
