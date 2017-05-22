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
    public class LoggerAdapter : LegacyLoggerAdapter, ILogger
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

        public override void LogDebug(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public override void LogError(string data)
        {
            ProjectLogger.Log(MessageLevel.Error, data);
        }

        public override void LogInformation(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public override void LogMinimal(string data)
        {
            ProjectLogger.Log(MessageLevel.Info, data);
        }

        public override void LogVerbose(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public override void LogWarning(string data)
        {
            ProjectLogger.Log(MessageLevel.Warning, data);
        }

        public override void LogInformationSummary(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }

        public override void LogErrorSummary(string data)
        {
            ProjectLogger.Log(MessageLevel.Debug, data);
        }
    }
}
