﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Shared;

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

        public static IAssetsLogMessage Create(IRestoreLogMessage logMessage)
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

        public bool LogEquals(IAssetsLogMessage other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Level == other.Level &&
                Code == other.Code &&
                WarningLevel == other.WarningLevel &&
                StartLineNumber == other.StartLineNumber &&
                EndLineNumber == other.EndColumnNumber &&
                StartColumnNumber == other.StartColumnNumber &&
                EndColumnNumber == other.EndColumnNumber &&
                ((Message == null && other.Message == null) || 
                    Message.Equals(other.Message, StringComparison.OrdinalIgnoreCase)) &&
                ((ProjectPath == null && other.ProjectPath == null) || 
                    ProjectPath.Equals(other.Message, StringComparison.OrdinalIgnoreCase)) &&
                ((FilePath == null && other.FilePath == null) || 
                    FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase)) &&
                ((LibraryId == null && other.LibraryId == null) || 
                    LibraryId.Equals(other.LibraryId, StringComparison.OrdinalIgnoreCase)))             
            {
                if (TargetGraphs != null && other.TargetGraphs != null )
                {
                    return TargetGraphs.OrderBy(t => t).SequenceEqual(other.TargetGraphs.OrderBy(t => t), StringComparer.Ordinal);
                }
            }

            return false;
        }
    }
}
