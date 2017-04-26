﻿using System;
using System.Threading.Tasks;
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build
{
    /// <summary>
    /// TaskLoggingHelper -> ILogger
    /// </summary>
    internal class MSBuildLogger : Common.ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;

        public MSBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging;
        }

        public void Log(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public void Log(ILogMessage message)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task LogAsync(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            throw new NotImplementedException();
        }

        public void LogDebug(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public void LogError(string data)
        {
            _taskLogging.LogError(data);
        }

        public void LogErrorSummary(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public void LogInformation(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Normal, data);
        }

        public void LogInformationSummary(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public void LogMinimal(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public void LogVerbose(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public void LogWarning(string data)
        {
            _taskLogging.LogWarning(data);
        }
    }
}