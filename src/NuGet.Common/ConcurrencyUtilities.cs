// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    internal static class ConcurrencyUtilities
    {
        internal static async Task<TVal> ExecuteWithFileLocked<TVal>(string filePath,
            Func<CancellationToken, Task<TVal>> action,
            CancellationToken token)
        {
            TVal result = default(TVal);

            var name = FilePathToLockName(filePath);

            var lockStart = new SemaphoreSlim(initialCount: 0, maxCount: 1);
            var lockEnd = new SemaphoreSlim(initialCount: 0, maxCount: 1);

            // We are creating threads below, instead of simply using a ThreadPool thread using Task.Run,
            // in order to avoid ThreadPool exhaustion. By using Task.Run here, we will have to reduce
            // the maximum number of Tasks the caller can create by a factor of 2
            // This gives us both the performance we desire and does not cause ThreadPool exhaustion
            var threadStart = new ThreadStart(() => HandleMutex(name, lockStart, lockEnd, token));

            var thread = new Thread(threadStart)
            {
                Name = "Mutex+" + name
            };

            thread.Start();

            try
            {
                await lockStart.WaitAsync(token);

                token.ThrowIfCancellationRequested();

                result = await action(token);
            }
            finally
            {
                lockEnd.Release();
            }

            return result;
        }

        private static void HandleMutex(string name,
            SemaphoreSlim lockStart,
            SemaphoreSlim lockEnd,
            CancellationToken token)
        {
            using (var mutex = new Mutex(initiallyOwned: false, name: name))
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (mutex.WaitOne(1000))
                        {
                            try
                            {
                                lockStart.Release();
                            }
                            finally
                            {
                                try
                                {
                                    lockEnd.Wait();
                                }
                                finally
                                {
                                    mutex.ReleaseMutex();
                                }
                            }

                            break;
                        }

                        // The mutex is not released. Loop continues
                    }
                    catch (AbandonedMutexException)
                    {
                        // The mutex was abandoned, possibly because the process holding the mutex was killed.
                    }
                }
            }
        }

        private static string FilePathToLockName(string filePath)
        {
            // If we use a file path directly as the name of a semaphore,
            // the ctor of semaphore looks for the file and throws an IOException
            // when the file doesn't exist. So we need a conversion from a file path
            // to a unique lock name.
            return filePath.Replace(Path.DirectorySeparatorChar, '_');
        }
    }
}
