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
        internal static async Task<T> ExecuteWithFileLocked<T>(string filePath, Func<Task<T>> action)
        {
            T result = default(T);

            using (var filelock = new Mutex(initiallyOwned: false, name: FilePathToLockName(filePath)))
            {
                while (true)
                {
                    try
                    {
                        if (filelock.WaitOne(1000))
                        {
                            try
                            {
                                result = await action();
                            }
                            finally
                            {
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

            return result;
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
