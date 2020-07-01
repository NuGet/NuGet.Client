// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.VisualStudio.Telemetry;
using Task = System.Threading.Tasks.Task;

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

        [SuppressMessage("Build", "CA2213:'OutputConsoleLogger' contains field '_semaphore' that is of IDisposable type 'ReentrantSemaphore', but it is never disposed. Change the Dispose method on 'OutputConsoleLogger' to call Close or Dispose on this field.", Justification = "Field is disposed from async task invoked from Dispose.")]
        private readonly ReentrantSemaphore _semaphore = ReentrantSemaphore.Create(0, NuGetUIThreadHelper.JoinableTaskFactory.Context, ReentrantSemaphore.ReentrancyMode.NotAllowed);

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

            Run(nameof(OutputConsoleLogger), async () =>
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
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await _semaphore.ExecuteAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ErrorListTableDataSource.Value.Dispose();
                });

                _semaphore.Dispose();
            }
            ).FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(OutputConsoleLogger), nameof(Dispose)));
        }

        public void End()
        {
            Run(nameof(End), async () =>
            {
                await OutputConsole.WriteLineAsync(Resources.Finished);
                await OutputConsole.WriteLineAsync(string.Empty);

                // Give the error list focus
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorListTableDataSource.Value.BringToFrontIfSettingsPermit();
            });
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            Run($"{nameof(Log)}/{nameof(String)}", async () =>
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

                    await OutputConsole.WriteLineAsync(message);
                }
            });
        }

        public void Log(ILogMessage message)
        {
            Run($"{nameof(Log)}/{nameof(ILogMessage)}", async () =>
            {
                if (message.Level == LogLevel.Information
                    || message.Level == LogLevel.Error
                    || message.Level == LogLevel.Warning
                    || _verbosityLevel > DefaultVerbosityLevel)
                {
                    await OutputConsole.WriteLineAsync(message.FormatWithCode());

                    if (message.Level == LogLevel.Error ||
                        message.Level == LogLevel.Warning)
                    {
                        ReportError(message);
                    }
                }
            });
        }

        public void Start()
        {
            Run(nameof(Start), async () =>
            {
                await OutputConsole.ActivateAsync();
                await OutputConsole.ClearAsync();
                _verbosityLevel = await GetMSBuildVerbosityLevelAsync();

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorListTableDataSource.Value.ClearNuGetEntries();
            });
        }

        private async Task<int> GetMSBuildVerbosityLevelAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var properties = _dte.get_Properties(DTEEnvironmentCategory, DTEProjectPage);
            var value = properties.Item(MSBuildVerbosityKey).Value;
            if (value is int)
            {
                return (int)value;
            }

            return DefaultVerbosityLevel;
        }

        public void ReportError(string message)
        {
            Run($"{nameof(ReportError)}/{nameof(String)}", async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var errorListEntry = new ErrorListTableEntry(message, LogLevel.Error);
                ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry);
            });
        }

        public void ReportError(ILogMessage message)
        {
            Run($"{nameof(ReportError)}/{nameof(ILogMessage)}", async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var errorListEntry = new ErrorListTableEntry(message);
                ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry);
            });
        }

        private void Run(string methodName, Func<Task> action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory
                               .RunAsync(() => _semaphore.ExecuteAsync(action))
                               .FileAndForget(TelemetryUtility.CreateFileAndForgetEventName(nameof(OutputConsoleLogger), methodName));
        }
    }
}
