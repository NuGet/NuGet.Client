﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public class RestoreLogMessage : IRestoreLogMessage
    {
        public LogLevel Level { get; set; }
        public NuGetLogCode Code { get; set; }
        public string Message { get; set; }
        public DateTimeOffset Time { get; set; }
        public string ProjectPath { get; set; }
        public WarningLevel WarningLevel { get; set; } = WarningLevel.Severe; //setting default to Severe as 0 implies show no warnings
        public string FilePath { get; set; }
        public int StartLineNumber { get; set; }
        public int StartColumnNumber { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumnNumber { get; set; }
        public string LibraryId { get; set; }
        public IReadOnlyList<string> TargetGraphs { get; set; }
        public bool ShouldDisplay { get; set; }


        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode,
            string errorString, string targetGraph, bool logToInnerLogger)
        {
            Level = logLevel;
            Code = errorCode;
            Message = errorString;
            Time = DateTimeOffset.UtcNow;

            var graphList = new List<string>();

            if (!string.IsNullOrEmpty(targetGraph))
            {
                graphList.Add(targetGraph);
            }

            TargetGraphs = graphList;
            ShouldDisplay = logToInnerLogger;
        }

        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode, 
            string errorString, string targetGraph)
            : this(logLevel, errorCode, errorString, targetGraph, false)
        {
        }

        public RestoreLogMessage(LogLevel logLevel, NuGetLogCode errorCode, string errorString)
            : this(logLevel, errorCode, errorString, string.Empty, false)
        {
        }

        public RestoreLogMessage(LogLevel logLevel, string errorString)
            : this(logLevel, LogLevel.Error == logLevel ? NuGetLogCode.NU1000 : NuGetLogCode.NU1500, errorString, string.Empty, false)
        {
        }

        public RestoreLogMessage(LogLevel logLevel, string errorString, bool logToInnerLogger)
            : this(logLevel, LogLevel.Error == logLevel ? NuGetLogCode.NU1000 : NuGetLogCode.NU1500, errorString, string.Empty, logToInnerLogger)
        {
        }

        /// <summary>
        /// Create a log message for a target graph library.
        /// </summary>
        public static RestoreLogMessage CreateWarning(
            NuGetLogCode code,
            string message,
            string libraryId,
            params string[] targetGraphs)
        {
            return new RestoreLogMessage(LogLevel.Warning, message)
            {
                Code = code,
                LibraryId = libraryId,
                TargetGraphs = targetGraphs.ToList()
            };
        }

        /// <summary>
        /// Create a warning log message.
        /// </summary>
        public static RestoreLogMessage CreateWarning(
            NuGetLogCode code,
            string message)
        {
            return new RestoreLogMessage(LogLevel.Warning, code, message);
        }

        /// <summary>
        /// Create an error log message.
        /// </summary>
        public static RestoreLogMessage CreateError(
            NuGetLogCode code,
            string message)
        {
            return new RestoreLogMessage(LogLevel.Error, code, message);
        }

        /// <summary>
        /// Create an error log message for a target graph.
        /// </summary>
        public static RestoreLogMessage CreateError(
            NuGetLogCode code,
            string message,
            string libraryId,
            params string[] targetGraphs)
        {
            return new RestoreLogMessage(LogLevel.Error, message)
            {
                Code = code,
                LibraryId = libraryId,
                TargetGraphs = targetGraphs.ToList()
            };
        }
    }
}
