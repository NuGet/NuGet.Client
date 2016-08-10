// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class ToolRestoreResult : IRestoreResult
    {
        public ToolRestoreResult(
            string toolName,
            bool success,
            IEnumerable<RestoreTargetGraph> graphs,
            LockFileTarget lockFileTarget,
            LockFileTargetLibrary fileTargetLibrary,
            string lockFilePath,
            LockFile lockFile,
            LockFile previousLockFile)
        {
            ToolName = toolName;
            Success = success;
            Graphs = graphs;
            LockFileTarget = lockFileTarget;
            FileTargetLibrary = fileTargetLibrary;
            LockFilePath = lockFilePath;
            LockFile = lockFile;
            PreviousLockFile = previousLockFile;
        }

        public string ToolName { get; }
        public bool Success { get; }
        public IEnumerable<RestoreTargetGraph> Graphs { get; }
        public LockFileTarget LockFileTarget { get; }
        public LockFileTargetLibrary FileTargetLibrary { get; }
        public string LockFilePath { get; set; }
        public LockFile LockFile { get; }
        public LockFile PreviousLockFile { get; }
    }
}