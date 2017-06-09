// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.ProjectModel
{
    public static class LockFileUtilities
    {
        /// <summary>
        /// Returns the lockfile if it exists, otherwise null.
        /// </summary>
        public static LockFile GetLockFile(string lockFilePath, Common.ILogger logger)
        {
            LockFile lockFile = null;

            if (File.Exists(lockFilePath))
            {
                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                lockFile = format.Read(lockFilePath, logger);
            }

            return lockFile;
        }

        public static async Task<LockFile> GetLockFileAsync(string lockFilePath, Common.ILogger logger)
        {
            LockFile lockFile = null;

            if (File.Exists(lockFilePath))
            {
                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                lockFile = await format.ReadWithLock(lockFilePath, logger);
            }

            return lockFile;
        }
    }
}
