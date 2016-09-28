﻿using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build.Tasks.Pack
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

        public void LogDebug(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public void LogError(string data)
        {
            // Log errors as warnings. Then log the error summary as actual errors.
            LogWarning(data);
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
            LogInformation(data);
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
