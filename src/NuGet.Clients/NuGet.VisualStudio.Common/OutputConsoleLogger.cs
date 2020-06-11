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
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio.Common
{
    [Export(typeof(INuGetUILogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class OutputConsoleLogger : INuGetUILogger, IDisposable
    {
        private const string DTEProjectPage = "ProjectsAndSolution";
        private const string DTEEnvironmentCategory = "Environment";
        private const string MSBuildVerbosityKey = "MSBuildOutputVerbosity";

        private const int DefaultVerbosityLevel = 2;
        private int _verbosityLevel;

        private AsyncLazy<EnvDTE.DTE> _dte;
        private AsyncLazy<IOutputConsole> _outputConsole;
        private AsyncLazy<ErrorListTableDataSource> _errorListTableDataSource;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private EnvDTE.BuildEvents _buildEvents;
        private EnvDTE.SolutionEvents _solutionEvents;

        [ImportingConstructor]
        public OutputConsoleLogger(
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListTableDataSource)
        {
            if (consoleProvider == null)
            {
                throw new ArgumentNullException(nameof(consoleProvider));
            }

            _dte = AsyncLazy.New(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await AsyncServiceProvider.GlobalProvider.GetDTEAsync();
            });

            _outputConsole = AsyncLazy.New(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await consoleProvider.CreatePackageManagerConsoleAsync();
            });

            _errorListTableDataSource = AsyncLazy.New(async () =>
            {
                var errorListTableDataSourceValue = errorListTableDataSource.Value;

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await _dte;
                _buildEvents = dte.Events.BuildEvents;
                _buildEvents.OnBuildBegin += (_, __) => { errorListTableDataSourceValue.ClearNuGetEntries(); };
                _solutionEvents = dte.Events.SolutionEvents;
                _solutionEvents.AfterClosing += () => { errorListTableDataSourceValue.ClearNuGetEntries(); };

                return errorListTableDataSourceValue;
            });
        }

        public void Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var errorListTableDataSource = await _errorListTableDataSource;
                errorListTableDataSource.Dispose();
            });
        }

        public void Start()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(StartAsync);
        }

        public async Task StartAsync()
        {
            var outputConsole = await _outputConsole;
            await outputConsole.ActivateAsync();
            await outputConsole.ClearAsync();
            _verbosityLevel = await GetMSBuildVerbosityLevelAsync();

            var errorListTableDataSource = await _errorListTableDataSource;
            errorListTableDataSource.ClearNuGetEntries();
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => LogAsync(level, message, args));
        }

        public async Task LogAsync(MessageLevel level, string message, params object[] args)
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

                var outputConsole = await _outputConsole;
                await outputConsole.WriteLineAsync(message);

            }
        }

        public void Log(ILogMessage message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => LogAsync(message));
        }

        public async Task LogAsync(ILogMessage message)
        {
            if (message.Level == LogLevel.Information
             || message.Level == LogLevel.Error
             || message.Level == LogLevel.Warning
             || _verbosityLevel > DefaultVerbosityLevel)
            {
                var outputConsole = await _outputConsole;
                await outputConsole.WriteLineAsync(message.FormatWithCode());

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

            var dte = await _dte;
            var properties = dte.get_Properties(DTEEnvironmentCategory, DTEProjectPage);
            var value = properties.Item(MSBuildVerbosityKey).Value;
            if (value is int)
            {
                return (int)value;
            }

            return DefaultVerbosityLevel;
        }

        public void ReportError(string message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => ReportErrorAsync(message));
        }

        public async Task ReportErrorAsync(string message)
        {
            var errorListEntry = new ErrorListTableEntry(message, LogLevel.Error);
            var errorListTableDataSource = await _errorListTableDataSource;
            errorListTableDataSource.AddNuGetEntries(errorListEntry);
        }

        public void ReportError(ILogMessage message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => ReportErrorAsync(message));
        }

        public async Task ReportErrorAsync(ILogMessage message)
        {
            var errorListEntry = new ErrorListTableEntry(message);
            var errorListTableDataSource = await _errorListTableDataSource;
            errorListTableDataSource.AddNuGetEntries(errorListEntry);
        }

        public void End()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(EndAsync);
        }

        public async Task EndAsync()
        {
            var outputConsole = await _outputConsole;
            await outputConsole.WriteLineAsync(Resources.Finished);
            await outputConsole.WriteLineAsync(string.Empty);

            // Give the error list focus
            var errorListTableDataSource = await _errorListTableDataSource;
            errorListTableDataSource.BringToFrontIfSettingsPermit();
        }
    }
}
