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
            Func<Task<TVal>> action,
            CancellationToken token)
        {
            return await action();

            //while (true)
            //{
            //    token.ThrowIfCancellationRequested();
            //    using (var fileLock = new Semaphore(initialCount: 1, maximumCount: 1, name: FilePathToLockName(filePath)))
            //    {
            //        if (fileLock.WaitOne(TimeSpan.FromSeconds(1)))
            //        {
            //            try
            //            {
            //                // Can perform the action
            //                return await action();
            //            }
            //            finally
            //            {
            //                fileLock.Release();
            //            }
            //        }

            //        // Timed out. Still the semaphore is not released. Loop continues
            //    }
            //}
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
