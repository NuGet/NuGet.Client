// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGetConsole;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Aggregates logging and UI services consumed by the <see cref="SolutionRestoreJob"/>.
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal sealed class RestoreOperationLogger : ILogger, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOutputConsoleProvider _outputConsoleProvider;

        private Lazy<ErrorListTableDataSource> _errorListDataSource;
        private RestoreOperationSource _operationSource;
        private JoinableTaskFactory _taskFactory;
        private CancellationTokenSource _externalCts;
        private Func<CancellationToken, Task<RestoreOperationProgressUI>> _progressFactory;
        private IOutputConsole _outputConsole;

        private bool _cancelled;
        private bool _hasHeaderBeenShown;
        private bool _showErrorList;

        // The value of the "MSBuild project build output verbosity" setting
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        public int OutputVerbosity { get; private set; }

        [ImportingConstructor]
        public RestoreOperationLogger(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IOutputConsoleProvider outputConsoleProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (outputConsoleProvider == null)
            {
                throw new ArgumentNullException(nameof(outputConsoleProvider));
            }

            _serviceProvider = serviceProvider;
            _outputConsoleProvider = outputConsoleProvider;
        }

        public async Task StartAsync(
            RestoreOperationSource operationSource,
            Lazy<ErrorListTableDataSource> errorListDataSource,
            JoinableTaskFactory jtf,
            CancellationTokenSource cts)
        {
            if (errorListDataSource == null)
            {
                throw new ArgumentNullException(nameof(errorListDataSource));
            }

            if (jtf == null)
            {
                throw new ArgumentNullException(nameof(jtf));
            }

            if (cts == null)
            {
                throw new ArgumentNullException(nameof(cts));
            }

            _operationSource = operationSource;
            _errorListDataSource = errorListDataSource;
            _taskFactory = jtf;
            _externalCts = cts;
            _externalCts.Token.Register(() => _cancelled = true);

#if VS14
            _progressFactory = t => WaitDialogProgress.StartAsync(_serviceProvider, _taskFactory, t);
#else
            _progressFactory = t => StatusBarProgress.StartAsync(_serviceProvider, _taskFactory, t);
#endif

            await _taskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OutputVerbosity = GetMSBuildOutputVerbositySetting();

                switch (_operationSource)
                {
                    case RestoreOperationSource.Implicit: // background auto-restore
                        _outputConsole = _outputConsoleProvider.CreatePackageManagerConsole();
                        break;
                    case RestoreOperationSource.OnBuild:
                        _outputConsole = _outputConsoleProvider.CreateBuildOutputConsole();
                        _outputConsole.Activate();
                        break;
                    case RestoreOperationSource.Explicit:
                        _outputConsole = _outputConsoleProvider.CreatePackageManagerConsole();
                        _outputConsole.Activate();
                        _outputConsole.Clear();
                        break;
                }
            });

            if (_errorListDataSource.IsValueCreated)
            {
                // Clear old entries
                _errorListDataSource.Value.ClearNuGetEntries();
            }
        }

        public Task StopAsync()
        {
            if (_showErrorList)
            {
                // Give the error list focus
                _errorListDataSource.Value.BringToFront();
            }

            return Task.FromResult(true);
        }

        public void LogDebug(string data)
        {
            LogToVS(VerbosityLevel.Diagnostic, data);
        }

        public void LogVerbose(string data)
        {
            LogToVS(VerbosityLevel.Detailed, data);
        }

        public void LogInformation(string data)
        {
            LogToVS(VerbosityLevel.Normal, data);
        }

        public void LogMinimal(string data)
        {
            LogInformation(data);
        }

        public void LogWarning(string data)
        {
            LogToVS(VerbosityLevel.Minimal, data);
        }

        public void LogError(string data)
        {
            LogToVS(VerbosityLevel.Quiet, data);
        }

        public void LogInformationSummary(string data)
        {
            // Treat Summary as Debug
            LogDebug(data);
        }

        public void LogErrorSummary(string data)
        {
            // Treat Summary as Debug
            LogDebug(data);
        }

        private void LogToVS(VerbosityLevel verbosityLevel, string message)
        {
            if (_cancelled)
            {
                // If an operation is canceled, don't log anything, simply return
                // And, show a single message gets shown in the summary that package restore has been canceled
                // Do not report it as separate errors
                return;
            }

            // If the verbosity level of message is worse than VerbosityLevel.Normal, that is,
            // VerbosityLevel.Detailed or VerbosityLevel.Diagnostic, AND,
            // _msBuildOutputVerbosity is lesser than verbosityLevel; do nothing
            if (verbosityLevel > VerbosityLevel.Normal && OutputVerbosity < (int)verbosityLevel)
            {
                return;
            }

            // VerbosityLevel.Quiet corresponds to ILogger.LogError, and,
            // VerbosityLevel.Minimal corresponds to ILogger.LogWarning
            // In these 2 cases, we add an error or warning to the error list window
            if (verbosityLevel == VerbosityLevel.Quiet ||
                verbosityLevel == VerbosityLevel.Minimal)
            {
                var errorLevel = verbosityLevel == VerbosityLevel.Quiet ? LogLevel.Error : LogLevel.Warning;
                var errorListEntry = new ErrorListTableEntry(message, errorLevel);

                // Add the entry to the list
                _errorListDataSource.Value.AddEntries(errorListEntry);

                // Display the error list after restore completes
                _showErrorList = true;
            }

            // Only show messages with VerbosityLevel.Normal. That is, info messages only.
            // Do not show errors, warnings, verbose or debug messages on the progress dialog
            // Avoid showing indented messages, these are typically not useful for the progress dialog since
            // they are missing the context of the parent text above it
            var reportProgress = RestoreOperationProgressUI.Current != null
                && verbosityLevel == VerbosityLevel.Normal
                && message.Length == message.TrimStart().Length;

            // Write to the output window if the verbosity level is high enough.
            var showAsOutputMessage = ShowMessageAsOutput(verbosityLevel);

            // Avoid moving to the UI thread unless there is work to do
            if (reportProgress || showAsOutputMessage)
            {
                // Run on the UI thread
                Do((_, progress) =>
                {
                    // Progress dialog
                    if (reportProgress)
                    {
                        progress?.ReportProgress(message);
                    }

                    // Output console
                    if (showAsOutputMessage)
                    {
                        WriteLine(verbosityLevel, message);
                    }
                });
            }
        }

        /// <summary>
        /// Outputs a message to the debug output pane, if the VS MSBuildOutputVerbosity
        /// setting value is greater than or equal to the given verbosity. So if verbosity is 0,
        /// it means the message is always written to the output pane.
        /// </summary>
        /// <param name="verbosity">The verbosity level.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">An array of objects to write using format. </param>
        public void WriteLine(VerbosityLevel verbosity, string format, params object[] args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ShowMessageAsOutput(verbosity))
            {
                _outputConsole.WriteLine(format, args);
            }
        }

        public bool ShowMessageAsOutput(VerbosityLevel verbosity)
        {
            return _outputConsole != null && OutputVerbosity >= (int)verbosity;
        }

        public Task LogExceptionAsync(Exception ex)
        {
            return DoAsync((_, __) =>
            {
                string message;
                if (OutputVerbosity < 3)
                {
                    message = string.Format(CultureInfo.CurrentCulture,
                        Resources.ErrorOccurredRestoringPackages,
                        ex.Message);
                }
                else
                {
                    // output exception detail when _msBuildOutputVerbosity is >= Detailed.
                    message = string.Format(CultureInfo.CurrentCulture, Resources.ErrorOccurredRestoringPackages, ex);
                }

                if (_operationSource == RestoreOperationSource.Explicit)
                {
                    // Write to the error window and console
                    LogError(message);
                }
                else
                {
                    // Write to console
                    WriteLine(VerbosityLevel.Quiet, message);
                }

                ExceptionHelper.WriteToActivityLog(ex);
            });
        }

        public void ShowError(string errorText)
        {
            var entry = new ErrorListTableEntry(errorText, LogLevel.Error);

            _errorListDataSource.Value.AddEntries(entry);
            _errorListDataSource.Value.BringToFront();
        }

        public Task WriteHeaderAsync()
        {
            if (!_hasHeaderBeenShown)
            {
                _hasHeaderBeenShown = true;

                return DoAsync((_, __) =>
                {
                    switch (_operationSource)
                    {
                        case RestoreOperationSource.Implicit:
                            WriteLine(VerbosityLevel.Quiet, Resources.RestoringPackages);
                            break;
                        case RestoreOperationSource.OnBuild:
                            WriteLine(VerbosityLevel.Quiet, Resources.PackageRestoreOptOutMessage);
                            break;
                        case RestoreOperationSource.Explicit:
                            WriteLine(VerbosityLevel.Quiet, Resources.RestoringPackages);
                            break;
                    }
                });
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public Task WriteSummaryAsync(NuGetOperationStatus operationStatus, TimeSpan duration)
        {
            var forceStatusWrite = _operationSource == RestoreOperationSource.Explicit;
            var quietOrMinimal = forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Minimal;
            var quietOrNormal = forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Normal;
            var quietOrDetailed = forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Detailed;

            return DoAsync((_, __) =>
            {
                switch (operationStatus)
                {
                    case NuGetOperationStatus.Cancelled:
                        WriteLine(
                            quietOrMinimal,
                            Resources.PackageRestoreCanceled);
                        break;
                    case NuGetOperationStatus.NoOp:
                        if (forceStatusWrite)
                        {
                            WriteLine(
                                quietOrDetailed,
                                Resources.NothingToRestore);
                        }
                        break;
                    case NuGetOperationStatus.Failed:
                        WriteLine(
                            quietOrMinimal,
                            Resources.PackageRestoreFinishedWithError);
                        break;
                    case NuGetOperationStatus.Succeeded:
                        WriteLine(
                            quietOrNormal,
                            Resources.PackageRestoreFinished);
                        break;
                }

                if (_operationSource != RestoreOperationSource.OnBuild && (_hasHeaderBeenShown || forceStatusWrite))
                {
                    WriteLine(
                        quietOrMinimal,
                        Resources.Operation_TotalTime,
                        duration);
                    WriteLine(quietOrMinimal, Resources.Operation_Finished);
                    WriteLine(quietOrMinimal, string.Empty);
                }
            });
        }

        /// <summary>
        /// Helper async method to run batch of logging call on the main UI thread.
        /// </summary>
        /// <param name="action">Sync callback invoking logger.</param>
        /// <returns>An awaitable task.</returns>
        public async Task DoAsync(Action<RestoreOperationLogger, RestoreOperationProgressUI> action)
        {
            // capture current progress from the current execution context
            var progress = RestoreOperationProgressUI.Current;

            await _taskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action(this, progress);
            });
        }

        /// <summary>
        /// Helper synchronous method to run batch of logging call on the main UI thread.
        /// </summary>
        /// <param name="action">Sync callback invoking logger.</param>
        public void Do(Action<RestoreOperationLogger, RestoreOperationProgressUI> action)
        {
            // capture current progress from the current execution context
            var progress = RestoreOperationProgressUI.Current;

            _taskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action(this, progress);
            });
        }

        public async Task RunWithProgressAsync(
            Func<RestoreOperationLogger, RestoreOperationProgressUI, CancellationToken, Task> asyncRunMethod,
            CancellationToken token)
        {
            using (var progress = await _progressFactory(token))
            using (var ctr = progress.RegisterUserCancellationAction(() => _externalCts.Cancel()))
            {
                // Save the progress instance in the current execution context.
                // The value won't be available outside of this async method.
                RestoreOperationProgressUI.Current = progress;

                await asyncRunMethod(this, progress, token);
            }
        }

        public Task RunWithProgressAsync(
            Action<RestoreOperationLogger, RestoreOperationProgressUI, CancellationToken> runAction,
            CancellationToken token)
        {
            return RunWithProgressAsync(
                (l, p, t) => { runAction(l, p, t); return Task.CompletedTask; },
                token);
        }

        /// <summary>
        /// Returns the value of the VisualStudio MSBuildOutputVerbosity setting.
        /// </summary>
        /// <remarks>
        /// 0 is Quiet, while 4 is diagnostic.
        /// </remarks>
        private int GetMSBuildOutputVerbositySetting()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = _serviceProvider.GetDTE();

            var properties = dte.get_Properties("Environment", "ProjectsAndSolution");
            var value = properties.Item("MSBuildOutputVerbosity").Value;
            if (value is int)
            {
                return (int)value;
            }
            return 0;
        }

        public void Dispose()
        {
            _externalCts?.Dispose();
        }

        private class WaitDialogProgress : RestoreOperationProgressUI
        {
            private readonly ThreadedWaitDialogHelper.Session _session;
            private readonly JoinableTaskFactory _taskFactory;

            private WaitDialogProgress(
                ThreadedWaitDialogHelper.Session session,
                JoinableTaskFactory taskFactory)
            {
                _session = session;
                _taskFactory = taskFactory;
                UserCancellationToken = _session.UserCancellationToken;
            }

            public static async Task<RestoreOperationProgressUI> StartAsync(
                IServiceProvider serviceProvider,
                JoinableTaskFactory jtf,
                CancellationToken token)
            {
                return await jtf.RunAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var waitDialogFactory = serviceProvider.GetService<
                        SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

                    var session = waitDialogFactory.StartWaitDialog(
                        waitCaption: Resources.DialogTitle,
                        initialProgress: new ThreadedWaitDialogProgressData(
                            Resources.RestoringPackages,
                            progressText: string.Empty,
                            statusBarText: string.Empty,
                            isCancelable: true,
                            currentStep: 0,
                            totalSteps: 0));

                    return new WaitDialogProgress(session, jtf);
                });
            }

            public override void Dispose()
            {
                _taskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _session.Dispose();
                });
            }

            public override void ReportProgress(
                string progressMessage,
                uint currentStep,
                uint totalSteps)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // When both currentStep and totalSteps are 0, we get a marquee on the dialog
                var progressData = new ThreadedWaitDialogProgressData(
                    progressMessage,
                    progressText: string.Empty,
                    statusBarText: string.Empty,
                    isCancelable: true,
                    currentStep: (int)currentStep,
                    totalSteps: (int)totalSteps);

                _session.Progress.Report(progressData);
            }
        }

        private class StatusBarProgress : RestoreOperationProgressUI
        {
            private static object icon = (short)Constants.SBAI_General;
            private readonly JoinableTaskFactory _taskFactory;
            private readonly IVsStatusbar StatusBar;
            private uint cookie = 0;

            private StatusBarProgress(
                IVsStatusbar statusBar,
                JoinableTaskFactory taskFactory)
            {
                StatusBar = statusBar;
                _taskFactory = taskFactory;
            }

            public static async Task<RestoreOperationProgressUI> StartAsync(
                IServiceProvider serviceProvider,
                JoinableTaskFactory jtf,
                CancellationToken token)
            {
                return await jtf.RunAsync(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var statusBar = serviceProvider.GetService<SVsStatusbar, IVsStatusbar>();

                    // Make sure the status bar is not frozen
                    int frozen;
                    statusBar.IsFrozen(out frozen);

                    if (frozen != 0)
                    {
                        statusBar.FreezeOutput(0);
                    }

                    statusBar.Animation(1, ref icon);

                    RestoreOperationProgressUI progress = new StatusBarProgress(statusBar, jtf);
                    progress.ReportProgress(Resources.RestoringPackages);

                    return progress;
                });
            }

            public override void Dispose()
            {
                _taskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    StatusBar.Animation(0, ref icon);
                    StatusBar.Progress(ref cookie, 0, "", 0, 0);
                    StatusBar.FreezeOutput(0);
                    StatusBar.Clear();
                });
            }

            public override void ReportProgress(
                string progressMessage,
                uint currentStep,
                uint totalSteps)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Make sure the status bar is not frozen
                int frozen;
                StatusBar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    StatusBar.FreezeOutput(0);
                }

                StatusBar.SetText(progressMessage);

                if (totalSteps != 0)
                {
                    StatusBar.Progress(ref cookie, 1, "", currentStep, totalSteps);
                }
            }
        }
    }
}
