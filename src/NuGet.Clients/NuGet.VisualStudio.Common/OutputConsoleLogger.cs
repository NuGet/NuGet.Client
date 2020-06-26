// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio.Common
{
    public sealed class OutputConsoleLogger : INuGetUILogger
    {
        private const int DefaultVerbosityLevel = 2;

        private int _verbosityLevel;

        private readonly IOutputConsole _outputConsole;
        private readonly ErrorListTableDataSource _errorListTableDataSource;

        public OutputConsoleLogger(
            IOutputConsole outputConsole,
            ErrorListTableDataSource errorListTableDataSource,
            int verbosityLevel)
        {
            _outputConsole = outputConsole;
            _errorListTableDataSource = errorListTableDataSource;
            _verbosityLevel = verbosityLevel;
        }

        void IDisposable.Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await DisposeAsync();
                GC.SuppressFinalize(this);
            });
        }

        private async Task DisposeAsync()
        {
            await _outputConsole.WriteLineAsync(Resources.Finished);
            await _outputConsole.WriteLineAsync(string.Empty);

            // Give the error list focus
            _errorListTableDataSource.BringToFrontIfSettingsPermit();
        }

        void INuGetUILogger.Log(MessageLevel level, string message, params object[] args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => LogAsync(level, message, args));
        }

        private async Task LogAsync(MessageLevel level, string message, params object[] args)
        {
            if (level == MessageLevel.Info
                || level == MessageLevel.Error
                || level == MessageLevel.Warning
                || _verbosityLevel > DefaultVerbosityLevel)
            {
                if (args.Length > 0)
                {
                    message = string.Format(CultureInfo.CurrentCulture, message, args);
                }

                await _outputConsole.WriteLineAsync(message);

            }
        }

        void INuGetUILogger.Log(ILogMessage message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => LogAsync(message));
        }

        private async Task LogAsync(ILogMessage message)
        {
            if (message.Level == LogLevel.Information
             || message.Level == LogLevel.Error
             || message.Level == LogLevel.Warning
             || _verbosityLevel > DefaultVerbosityLevel)
            {
                await _outputConsole.WriteLineAsync(message.FormatWithCode());

                if (message.Level == LogLevel.Error ||
                    message.Level == LogLevel.Warning)
                {
                    ((INuGetUILogger)this).ReportError(message);
                }
            }
        }

        void INuGetUILogger.ReportError(string message)
        {
            var errorListEntry = new ErrorListTableEntry(message, LogLevel.Error);
            _errorListTableDataSource.AddNuGetEntries(errorListEntry);
        }

        void INuGetUILogger.ReportError(ILogMessage message)
        {
            var errorListEntry = new ErrorListTableEntry(message);
            _errorListTableDataSource.AddNuGetEntries(errorListEntry);
        }
    }
}
