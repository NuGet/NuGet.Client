// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build
{
    /// <summary>
    /// TaskLoggingHelper -> ILogger
    /// </summary>
    internal class MSBuildLogger : LoggerBase, Common.ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;

        public MSBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging ?? throw new ArgumentNullException(nameof(taskLogging));
        }

        public override void Log(ILogMessage message)
        {
            if (DisplayMessage(message.Level))
            {
                var logMessage = message as IRestoreLogMessage;

                if (logMessage == null)
                {
                    logMessage = new RestoreLogMessage(message.Level, message.Message)
                    {
                        Code = message.Code,
                        FilePath = message.ProjectPath,
                        StartLineNumber = -1,
                        EndLineNumber = -1,
                        StartColumnNumber = -1,
                        EndColumnNumber = -1
                    };
                }

                switch (message.Level)
                {
                    case LogLevel.Error:
                        LogError(logMessage);
                        break;

                    case LogLevel.Warning:
                        LogWarning(logMessage);
                        break;

                    case LogLevel.Minimal:
                        LogMessage(MessageImportance.High, logMessage);
                        break;

                    case LogLevel.Information:
                        LogMessage(MessageImportance.Normal, logMessage);
                        break;

                    case LogLevel.Debug:
                    case LogLevel.Verbose:
                    default:
                        // Default to LogLevel.Debug and low importance
                        LogMessage(MessageImportance.Low, logMessage);
                        break;
                }
            }
        }

        private void LogMessage(
            MessageImportance importance,
            IRestoreLogMessage logMessage)
        {
            if (logMessage.Code > NuGetLogCode.Undefined)
            {
                // NuGet does not currently have a subcategory while throwing logs, hence string.Empty
                _taskLogging.LogMessage(string.Empty,
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    logMessage.FilePath,
                    logMessage.StartLineNumber,
                    logMessage.StartColumnNumber,
                    logMessage.EndLineNumber,
                    logMessage.EndColumnNumber,
                    importance,
                    logMessage.Message);
            }
            else
            {
                _taskLogging.LogMessage(importance, logMessage.Message);
            }
        }

        private void LogWarning(IRestoreLogMessage logMessage)
        {
            if (logMessage.Code > NuGetLogCode.Undefined)
            {
                // NuGet does not currently have a subcategory while throwing logs, hence string.Empty
                _taskLogging.LogWarning(string.Empty,
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    logMessage.FilePath,
                    logMessage.StartLineNumber,
                    logMessage.StartColumnNumber,
                    logMessage.EndLineNumber,
                    logMessage.EndColumnNumber,
                    logMessage.Message);
            }
            else
            {
                _taskLogging.LogWarning(logMessage.Message);
            }
        }

        private void LogError(IRestoreLogMessage logMessage)
        {
            if (logMessage.Code > NuGetLogCode.Undefined)
            {
                // NuGet does not currently have a subcategory while throwing logs, hence string.Empty
                _taskLogging.LogError(string.Empty,
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    logMessage.FilePath,
                    logMessage.StartLineNumber,
                    logMessage.StartColumnNumber,
                    logMessage.EndLineNumber,
                    logMessage.EndColumnNumber,
                    logMessage.Message);
            }
            else
            {
                _taskLogging.LogError(logMessage.Message);
            }
        }

        public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            Log(message);

            return System.Threading.Tasks.Task.FromResult(0);
        }
    }
}