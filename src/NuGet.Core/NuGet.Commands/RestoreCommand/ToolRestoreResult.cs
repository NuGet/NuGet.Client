// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class ToolRestoreResult
    {
        public ToolRestoreResult(
            string toolName,
            bool success,
            LockFileTarget lockFileTarget,
            LockFileTargetLibrary fileTargetLibrary,
            string lockFilePath,
            LockFile lockFile)
        {
            ToolName = toolName;
            Success = success;
            LockFileTarget = lockFileTarget;
            FileTargetLibrary = fileTargetLibrary;
            LockFilePath = lockFilePath;
            LockFile = lockFile;
        }

        public string ToolName { get; }
        public bool Success { get; }
        public LockFileTarget LockFileTarget { get; }
        public LockFileTargetLibrary FileTargetLibrary { get; }
        public string LockFilePath { get; }
        public LockFile LockFile { get; }
    }
}