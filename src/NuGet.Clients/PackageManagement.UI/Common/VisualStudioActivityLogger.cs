using System;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Logger routing messages into VS ActivityLog
    /// </summary>
    internal class VisualStudioActivityLogger : Common.ILogger
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
    }
}
