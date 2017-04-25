// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Logger routing messages into VS ActivityLog
    /// </summary>
    [Export, Export("VisualStudioActivityLogger", typeof(ILogger))]
    public sealed class VisualStudioActivityLogger : ILogger
    {
        private const string LogEntrySource = "NuGet Package Manager";

        public void LogDebug(string data) => ActivityLog.LogInformation(LogEntrySource, data);

        public void LogError(string data) => ActivityLog.LogError(LogEntrySource, data);

        public void LogInformation(string data) => ActivityLog.LogInformation(LogEntrySource, data);

        public void LogMinimal(string data) => ActivityLog.LogInformation(LogEntrySource, data);

        public void LogVerbose(string data) => ActivityLog.LogInformation(LogEntrySource, data);

        public void LogWarning(string data) => ActivityLog.LogWarning(LogEntrySource, data);
        
        public void LogInformationSummary(string data) => LogInformation(data);
        
        public void LogErrorSummary(string data) => LogError(data);

        public void Log(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task LogAsync(LogLevel level, string data)
        {
            throw new NotImplementedException();
        }

        public void Log(ILogMessage message)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
