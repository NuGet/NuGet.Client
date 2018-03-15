// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Security.Cryptography;
using System.Text;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public static class LockFileUtilities
    {
        public static LockFile GetLockFile(string lockFilePath, Common.ILogger logger, out byte[] hash)
        {
            LockFile lockFile = null;
            hash = null;

            if (File.Exists(lockFilePath))
            {
                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                var lockFileContents = FileUtility.SafeRead(filePath: lockFilePath, read: (stream, path) =>
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        return streamReader.ReadToEnd();
                    }
                });

                using (var sha = SHA256.Create())
                {
                    hash = sha.ComputeHash(Encoding.ASCII.GetBytes(lockFileContents));
                }

                using (var stringReader = new StringReader(lockFileContents))
                {
                    lockFile = format.Read(stringReader, logger, lockFilePath);
                }
            }

            return lockFile;
        }
    }
}
