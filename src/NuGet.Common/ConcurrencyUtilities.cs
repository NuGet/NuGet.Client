// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    internal static class ConcurrencyUtilities
    {
        internal static async Task<TVal> ExecuteWithFileLocked<TVal>(string filePath,
            Func<Task<TVal>> action,
            CancellationToken token)
        {
            TVal result = default(TVal);

            var name = FilePathToLockName(filePath);

            var lockStart = new SemaphoreSlim(0, 1);
            var lockEnd = new SemaphoreSlim(1, 1);

            var task = Task.Run(() => HandleMutex(name, lockStart, lockEnd, token));

            try
            {
                await lockStart.WaitAsync(token);

                token.ThrowIfCancellationRequested();

                result = await action();
            }
            finally
            {
                lockEnd.Release();
            }

            return result;
        }

        private static void HandleMutex(string name, SemaphoreSlim lockStart, SemaphoreSlim lockEnd, CancellationToken token)
        {
            try
            {
                using (var filelock = new Mutex(initiallyOwned: false, name: name))
                {
                    while (true)
                    {
                        try
                        {
                            if (filelock.WaitOne(1000))
                            {
                                try
                                {
                                    lockStart.Release();
                                }
                                finally
                                {
                                    lockEnd.Wait();
                                    filelock.ReleaseMutex();
                                }

                                // Job is done. break the loop
                                break;
                            }

                            // Still the mutex is not released. Loop continues
                        }
                        catch (AbandonedMutexException)
                        {
                            // Mutex was abandoned. Possibly, because, the process holding the mutex was killed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
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
