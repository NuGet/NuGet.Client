// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

        /// <summary>
        /// Converts the ILogMessage into a string that can be logged as-is into a Console.
        /// </summary>
        /// <returns>The string representation of the ILogMessage.</returns>
        string FormatMessage();

        /// <summary>
        /// Converts the ILogMessage into a string that can be logged as-is into a Console.
        /// </summary>
        /// <returns>The string representation of the ILogMessage.</returns>
        Task<string> FormatMessageAsync();

    }
}
