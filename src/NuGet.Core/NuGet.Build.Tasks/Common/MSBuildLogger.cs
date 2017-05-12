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
    internal class MSBuildLogger : LegacyLoggerAdapter, Common.ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;

        public MSBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging;
        }

        public override void LogDebug(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public override void LogError(string data)
        {
            _taskLogging.LogError(data);
        }

        public override void LogErrorSummary(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public override void LogInformation(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Normal, data);
        }

        public override void LogInformationSummary(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public override void LogMinimal(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public override void LogVerbose(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public override void LogWarning(string data)
        {
            _taskLogging.LogWarning(data);
        }
    }
}