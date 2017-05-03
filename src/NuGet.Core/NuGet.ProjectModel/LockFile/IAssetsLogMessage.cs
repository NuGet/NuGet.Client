// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public interface IAssetsLogMessage
    {
        LogLevel Level { get; }
         NuGetLogCode Code { get; }
         string Message { get; }
         DateTimeOffset Time { get; }
         string ProjectPath { get; }
         WarningLevel WarningLevel { get; }
         string FilePath { get; }
         int StartLineNumber { get; }
         int StartColumnNumber { get; }
         int EndLineNumber { get; }
         int EndColumnNumber { get; }
         string LibraryId { get; }
         IReadOnlyList<string> TargetGraphs { get; }

    }
}
