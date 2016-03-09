// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class ToolRestoreResult
    {
        public ToolRestoreResult(bool success, IEnumerable<RestoreTargetGraph> restoreGraphs, string lockFilePath, LockFile lockFile)
        {
            Success = success;
            RestoreGraphs = restoreGraphs;
            LockFilePath = lockFilePath;
            LockFile = lockFile;
        }
        
        public bool Success { get; }
        public IEnumerable<RestoreTargetGraph> RestoreGraphs { get; }
        public string LockFilePath { get; }
        public LockFile LockFile { get; }
    }
}