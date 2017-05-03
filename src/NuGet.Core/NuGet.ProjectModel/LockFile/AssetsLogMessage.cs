// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    class AssetsLogMessage : IAssetsLogMessage
    {

        public LogLevel Level { get; internal set; }
        public NuGetLogCode Code { get; internal set; }
        public string Message { get; internal set; }
        public DateTimeOffset Time { get; internal set; }
        public string ProjectPath { get; internal set; }
        public WarningLevel WarningLevel { get; internal set; }
        public string FilePath { get; internal set; }
        public int StartLineNumber { get; internal set; }
        public int StartColumnNumber { get; internal set; }
        public int EndLineNumber { get; internal set; }
        public int EndColumnNumber { get; internal set; }
        public string LibraryId { get; internal set; }
        public IReadOnlyList<string> TargetGraphs { get; internal set; }

        public string FormatMessage()
        {
            // Only errors and warnings need codes. informational do not need codes.
            if (Level >= LogLevel.Warning)
            {
                return $"{Enum.GetName(typeof(NuGetLogCode), Code)}: {Message}";
            }
            else
            {
                return Message;
            }
        }

        public Task<string> FormatMessageAsync()
        {
            return Task.FromResult(FormatMessage());
        }

        public AssetsLogMessage(LogLevel logLevel, NuGetLogCode errorCode,
            string errorString, string targetGraph)
        {
            Level = logLevel;
            Code = errorCode;
            Message = errorString;
            Time = DateTimeOffset.Now;

            if (!string.IsNullOrEmpty(targetGraph))
            {
                TargetGraphs = new List<string>
                {
                    targetGraph
                };
            }
        }

        public AssetsLogMessage(LogLevel logLevel, NuGetLogCode errorCode, string errorString)
            : this(logLevel, errorCode, errorString, string.Empty)
        {
        }

        public AssetsLogMessage(LogLevel logLevel, string errorString)
            : this(logLevel, LogLevel.Error == logLevel ? NuGetLogCode.NU1000 : NuGetLogCode.NU1500, errorString, string.Empty)
        {
        }
    }
}
