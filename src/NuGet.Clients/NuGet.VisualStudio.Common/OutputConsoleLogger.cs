// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio.Common
{
    [Export(typeof(INuGetUILogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class OutputConsoleLogger : INuGetUILogger, IDisposable
    {
        private const string LogEntrySource = "NuGet Package Manager";
        private const string DTEProjectPage = "ProjectsAndSolution";
        private const string DTEEnvironmentCategory = "Environment";
        private const string MSBuildVerbosityKey = "MSBuildOutputVerbosity";

        private const int DefaultVerbosityLevel = 2;
        private int _verbosityLevel;
        private EnvDTE.DTE _dte;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private EnvDTE.BuildEvents _buildEvents;
        private EnvDTE.SolutionEvents _solutionEvents;

        public IOutputConsole OutputConsole { get; private set; }

        public Lazy<ErrorListTableDataSource> ErrorListTableDataSource { get; private set; }

        [ImportingConstructor]
        public OutputConsoleLogger(
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListDataSource)
            : this(AsyncServiceProvider.GlobalProvider, consoleProvider, errorListDataSource)
        { }

        public OutputConsoleLogger(
            IAsyncServiceProvider asyncServiceProvider,
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListDataSource)
        {
            if (asyncServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(asyncServiceProvider));
            }

            if (consoleProvider == null)
            {
                throw new ArgumentNullException(nameof(consoleProvider));
            }

            ErrorListTableDataSource = errorListDataSource;

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _dte = await asyncServiceProvider.GetDTEAsync();
                _buildEvents = _dte.Events.BuildEvents;
                _buildEvents.OnBuildBegin += (_, __) => { ErrorListTableDataSource.Value.ClearNuGetEntries(); };
                _solutionEvents = _dte.Events.SolutionEvents;
                _solutionEvents.AfterClosing += () => { ErrorListTableDataSource.Value.ClearNuGetEntries(); };
                OutputConsole = await consoleProvider.CreatePackageManagerConsoleAsync();
            });
        }

        public void Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorListTableDataSource.Value.Dispose();
            });
        }

        public void End()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await OutputConsole.WriteLineAsync(Resources.Finished);
                await OutputConsole.WriteLineAsync(string.Empty);

                // Give the error list focus
                ErrorListTableDataSource.Value.BringToFrontIfSettingsPermit();
            });
        }

        public void Log(MessageLevel level, string message, params object[] args)
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

                NuGetUIThreadHelper.JoinableTaskFactory.Run(() => OutputConsole.WriteLineAsync(message));
            }
        }

        public void Log(ILogMessage message)
        {
            if (message.Level == LogLevel.Information
                || message.Level == LogLevel.Error
                || message.Level == LogLevel.Warning
                || _verbosityLevel > DefaultVerbosityLevel)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(() => OutputConsole.WriteLineAsync(message.FormatWithCode()));

                if (message.Level == LogLevel.Error ||
                    message.Level == LogLevel.Warning)
                {
                    ReportError(message);
                }
            }
        }

        private async Task<int> GetMSBuildVerbosityLevelAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            int verbosity = DefaultVerbosityLevel;

            try 
            {
                var properties = _dte.get_Properties(DTEEnvironmentCategory, DTEProjectPage);
                var val = properties.Item(MSBuildVerbosityKey).Value;
                if (val is int val_int)
                {
                    verbosity = val_int;
                }
            }
            catch
            {
                verbosity = DefaultVerbosityLevel;
            }

            return verbosity;
        }

        public void Start()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await OutputConsole.ActivateAsync();
                await OutputConsole.ClearAsync();
                _verbosityLevel = await GetMSBuildVerbosityLevelAsync();
                ErrorListTableDataSource.Value.ClearNuGetEntries();
            });
        }

        public void ReportError(string message)
        {
            var errorListEntry = new ErrorListTableEntry(message, LogLevel.Error);
            ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry);
        }

        public void ReportError(ILogMessage message)
        {
            var errorListEntry = new ErrorListTableEntry(message);
            ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry);
        }
    }
}
