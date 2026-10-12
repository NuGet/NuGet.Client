// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build
{
    /// <summary>
    /// TaskLoggingHelper -> ILogger
    /// </summary>
#if PACK_TASKS
    internal
#else
    public
#endif
    class MSBuildLogger : LoggerBase, Common.ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (string File, int Line, int Column)>> _packageReferenceLocations =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, (string File, int Line, int Column)>>(PathUtility.GetStringComparerBasedOnOS());

        private delegate void LogMessageWithDetails(string subcategory,
            string code,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            MessageImportance importance,
            string message,
            params object[] messageArgs);

        private delegate void LogErrorWithDetails(string subcategory,
            string code,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs);

        private delegate void LogMessageAsString(MessageImportance importance,
            string message,
            params object[] messageArgs);

        private delegate void LogErrorAsString(string message,
            params object[] messageArgs);

        public MSBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging ?? throw new ArgumentNullException(nameof(taskLogging));
        }

        internal void AddPackageReferenceLocation(string projectPath, string packageId, (string File, int Line, int Column) location)
        {
            if (!string.IsNullOrEmpty(location.File))
            {
                _packageReferenceLocations.GetOrAdd(projectPath,
                    _ => new ConcurrentDictionary<string, (string File, int Line, int Column)>(StringComparer.OrdinalIgnoreCase)).TryAdd(packageId, location);
            }
        }

        public override void Log(ILogMessage message)
        {
            if (DisplayMessage(message.Level))
            {
                if (RuntimeEnvironmentHelper.IsMono)
                {
                    LogForMono(message);
                    return;
                }
                else
                {
                    var logMessage = message as INuGetLogMessage;

                    if (logMessage == null)
                    {
                        logMessage = new RestoreLogMessage(message.Level, message.Message)
                        {
                            Code = message.Code,
                            FilePath = message.ProjectPath,
                            ProjectPath = message.ProjectPath,
                        };
                    }
                    LogForNonMono(logMessage);
                }
            }
        }

        /// <summary>
        /// Log using with metadata for non mono platforms.
        /// </summary>
        private void LogForNonMono(INuGetLogMessage message)
        {
            switch (message.Level)
            {
                case LogLevel.Error:
                    LogError(message, _taskLogging.LogError, _taskLogging.LogError);
                    break;

                case LogLevel.Warning:
                    LogError(message, _taskLogging.LogWarning, _taskLogging.LogWarning);
                    break;

                case LogLevel.Minimal:
                    LogMessage(message, MessageImportance.High, _taskLogging.LogMessage, _taskLogging.LogMessage);
                    break;

                case LogLevel.Information:
                    LogMessage(message, MessageImportance.Normal, _taskLogging.LogMessage, _taskLogging.LogMessage);
                    break;

                case LogLevel.Debug:
                case LogLevel.Verbose:
                default:
                    // Default to LogLevel.Debug and low importance
                    LogMessage(message, MessageImportance.Low, _taskLogging.LogMessage, _taskLogging.LogMessage);
                    break;
            }
        }

        /// <summary>
        /// Log using basic methods to avoid missing methods on mono.
        /// </summary>
        private void LogForMono(ILogMessage message)
        {
            switch (message.Level)
            {
                case LogLevel.Error:
                    _taskLogging.LogError(message.Message);
                    break;

                case LogLevel.Warning:
                    _taskLogging.LogWarning(message.Message);
                    break;

                case LogLevel.Minimal:
                    _taskLogging.LogMessage(MessageImportance.High, message.Message);
                    break;

                case LogLevel.Information:
                    _taskLogging.LogMessage(MessageImportance.Normal, message.Message);
                    break;

                case LogLevel.Debug:
                case LogLevel.Verbose:
                default:
                    // Default to LogLevel.Debug and low importance
                    _taskLogging.LogMessage(MessageImportance.Low, message.Message);
                    break;
            }

            return;
        }

        private static void LogMessage(INuGetLogMessage logMessage,
            MessageImportance importance,
            LogMessageWithDetails logWithDetails,
            LogMessageAsString logAsString)
        {
            if (logMessage.Code > NuGetLogCode.Undefined)
            {
                // NuGet does not currently have a subcategory while throwing logs, hence string.Empty
                logWithDetails(string.Empty,
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
                logAsString(importance, logMessage.Message);
            }
        }

        private void LogError(INuGetLogMessage logMessage,
            LogErrorWithDetails logWithDetails,
            LogErrorAsString logAsString)
        {
            if (logMessage.Code > NuGetLogCode.Undefined)
            {
                string file = logMessage.FilePath;
                int line = logMessage.StartLineNumber;
                int column = logMessage.StartColumnNumber;
                if (logMessage is IRestoreLogMessage { Code: NuGetLogCode.NU1510, ProjectPath: { } projectPath, LibraryId: { } packageId }
                    && _packageReferenceLocations.TryGetValue(projectPath, out var locations)
                    && locations.TryGetValue(packageId, out var location))
                {
                    file = location.File;
                    line = location.Line;
                    column = location.Column;
                }

                // NuGet does not currently have a subcategory while throwing logs, hence string.Empty
                logWithDetails(string.Empty,
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                    file,
                    line,
                    column,
                    logMessage.EndLineNumber,
                    logMessage.EndColumnNumber,
                    logMessage.Message);
            }
            else
            {
                logAsString(logMessage.Message);
            }
        }

        public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            Log(message);

            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
