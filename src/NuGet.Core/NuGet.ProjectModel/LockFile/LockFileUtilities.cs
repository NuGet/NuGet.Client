// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public static class LockFileUtilities
    {
        private static readonly List<string> LockFiles = new List<string>();

        public static LockFile GetLockFile(string lockFilePath, Common.ILogger logger)
        {
            return GetLockFile(lockFilePath, logger, LockFileFlags.All);
        }

        public static LockFile GetLockFile(string lockFilePath, Common.ILogger logger, LockFileFlags flags)
        {
            LockFile lockFile = null;

            if (File.Exists(lockFilePath))
            {
                LockFiles.Add(lockFilePath);

                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                lockFile = FileUtility.SafeRead(filePath: lockFilePath, read: (stream, path) => format.Read(stream, logger, path, flags));
            }

            return lockFile;
        }
    }

    [Flags]
    public enum LockFileFlags
    {
        Libraries = 1,
        Targets = 2,
        ProjectFileDependencyGroups = 4,
        PackageFolders = 8,
        PackageSpec = 16,
        CentralTransitiveDependencyGroups = 32,
        LogMessages = 64,

        All = 127,
    }
}
