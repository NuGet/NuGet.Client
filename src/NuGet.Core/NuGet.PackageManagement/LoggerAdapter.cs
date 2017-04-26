﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// ILogger -> INuGetProjectContext
    /// </summary>
    public class LoggerAdapter : ILogger
    {
        public INuGetProjectContext ProjectLogger { get; }

        public LoggerAdapter(INuGetProjectContext projectLogger)
        {
            if (projectLogger == null)
            {
                throw new ArgumentNullException(nameof(projectLogger));
            }

            ProjectLogger = projectLogger;
        }

        public void LogDebug(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public void LogError(string data)
        {
            ProjectLogger.Log(MessageLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public void LogMinimal(string data)
        {
            ProjectLogger.Log(MessageLevel.Info, data);
        }

        public void LogVerbose(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public void LogWarning(string data)
        {
            ProjectLogger.Log(MessageLevel.Warning, data);
        }

        public void LogInformationSummary(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public void LogErrorSummary(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public void Log(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public Task LogAsync(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public void Log(ILogMessage message)
        {
            throw new NotImplementedException();
        }

        public Task LogAsync(ILogMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
