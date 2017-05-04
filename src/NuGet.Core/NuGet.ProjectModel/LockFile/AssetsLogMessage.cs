// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public class AssetsLogMessage : IAssetsLogMessage
    {

        public LogLevel Level { get; set; }
        public NuGetLogCode Code { get; set; }
        public string Message { get; set; }
        public string ProjectPath { get; set; }
        public WarningLevel WarningLevel { get; set; }
        public string FilePath { get; set; }
        public string LibraryId { get; set; }
        public IReadOnlyList<string> TargetGraphs { get; set; }
        public int StartLineNumber { get; set; } = -1;
        public int StartColumnNumber { get; set; } = -1;
        public int EndLineNumber { get; set; } = -1;
        public int EndColumnNumber { get; set; } = -1;

        public static IAssetsLogMessage CreateAssetsLogMessage(IRestoreLogMessage logMessage)
        {
            return new AssetsLogMessage(logMessage.Level, logMessage.Code, logMessage.Message)
            {
                ProjectPath = logMessage.ProjectPath,
                WarningLevel = logMessage.WarningLevel,
                FilePath = logMessage.FilePath,
                LibraryId = logMessage.LibraryId,
                TargetGraphs = logMessage.TargetGraphs,
                StartLineNumber = logMessage.StartLineNumber,
                StartColumnNumber = logMessage.StartColumnNumber,
                EndLineNumber = logMessage.EndLineNumber,
                EndColumnNumber = logMessage.EndColumnNumber
            };
        }

        public AssetsLogMessage(LogLevel logLevel, NuGetLogCode errorCode,
            string errorString, string targetGraph)
        {
            Level = logLevel;
            Code = errorCode;
            Message = errorString;

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

    }
}
