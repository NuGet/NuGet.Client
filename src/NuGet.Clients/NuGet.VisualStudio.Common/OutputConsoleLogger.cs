// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Telemetry;
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
        private IVisualStudioShell _visualStudioShell;

        [SuppressMessage("Build", "CA2213:'OutputConsoleLogger' contains field '_semaphore' that is of IDisposable type 'ReentrantSemaphore', but it is never disposed. Change the Dispose method on 'OutputConsoleLogger' to call Close or Dispose on this field.", Justification = "Field is disposed from async task invoked from Dispose.")]
        private readonly ReentrantSemaphore _semaphore = ReentrantSemaphore.Create(1, NuGetUIThreadHelper.JoinableTaskFactory.Context, ReentrantSemaphore.ReentrancyMode.NotAllowed);

        public IOutputConsole OutputConsole { get; private set; }

        public Lazy<ErrorListTableDataSource> ErrorListTableDataSource { get; private set; }

        [ImportingConstructor]
        public OutputConsoleLogger(
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListDataSource)
            : this(
                  new VisualStudioShell(AsyncServiceProvider.GlobalProvider),
                  consoleProvider,
                  errorListDataSource)
        {
        }

        internal OutputConsoleLogger(
            IVisualStudioShell visualStudioShell,
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListDataSource)
        {
            Verify.ArgumentIsNotNull(visualStudioShell, nameof(visualStudioShell));
            Verify.ArgumentIsNotNull(consoleProvider, nameof(consoleProvider));
            Verify.ArgumentIsNotNull(errorListDataSource, nameof(errorListDataSource));

            _visualStudioShell = visualStudioShell;
            ErrorListTableDataSource = errorListDataSource;

            Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _visualStudioShell.SubscribeToBuildBeginAsync(() => ErrorListTableDataSource.Value.ClearNuGetEntries());
                await _visualStudioShell.SubscribeToAfterClosingAsync(() => ErrorListTableDataSource.Value.ClearNuGetEntries());
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
            ).PostOnFailure(nameof(OutputConsoleLogger), nameof(Dispose));
        }

        public void End()
        {
            Run(async () =>
            {
                await OutputConsole.WriteLineAsync(Resources.Finished);
                await OutputConsole.WriteLineAsync(string.Empty);

                // Give the error list focus
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorListTableDataSource.Value.BringToFrontIfSettingsPermit();
            });
        }

        public void Log(ILogMessage message)
        {
            Run(async () =>
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
                        await ReportAsync(message);
                    }
                }
            });
        }

        public void Start()
        {
            Run(async () =>
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

            var value = await _visualStudioShell.GetPropertyValueAsync(DTEEnvironmentCategory, DTEProjectPage, MSBuildVerbosityKey);
            if (value is int)
            {
                return (int)value;
            }

            return DefaultVerbosityLevel;
        }

        public void ReportError(ILogMessage message)
        {
            Run(() => ReportAsync(message));
        }

        private async Task ReportAsync(ILogMessage message)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var errorListEntry = new ErrorListTableEntry(message);
            ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry);
        }

        private void Run(Func<Task> action, [CallerMemberName] string methodName = null)
        {
            NuGetUIThreadHelper.JoinableTaskFactory
                               .RunAsync(() => _semaphore.ExecuteAsync(action))
                               .PostOnFailure(nameof(OutputConsoleLogger), methodName);
        }
    }
}
