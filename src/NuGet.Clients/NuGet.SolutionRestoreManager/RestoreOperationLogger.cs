// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using MSBuildVerbosityLevel = NuGet.SolutionRestoreManager.VerbosityLevel;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Aggregates logging and UI services consumed by the <see cref="SolutionRestoreJob"/>.
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal sealed class RestoreOperationLogger : LoggerBase, ILogger, IDisposable
    {
        private readonly IAsyncServiceProvider _asyncServiceProvider;
        private readonly IOutputConsoleProvider _outputConsoleProvider;

        // Queue of (bool reportProgress, bool showAsOutputMessage, ILogMessage logMessage)
        private readonly ConcurrentQueue<Tuple<bool, bool, ILogMessage>> _loggedMessages = new ConcurrentQueue<Tuple<bool, bool, ILogMessage>>();

        private Lazy<ErrorListTableDataSource> _errorListDataSource;
        private RestoreOperationSource _operationSource;
        private JoinableTaskFactory _taskFactory;
        private JoinableTaskCollection _jtc;

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
            IOutputConsoleProvider outputConsoleProvider)
            : this(AsyncServiceProvider.GlobalProvider, outputConsoleProvider)
        { }

        // Set the base logger to debug level, all filter will be done here.

        public RestoreOperationLogger(
            IAsyncServiceProvider asyncServiceProvider,
            IOutputConsoleProvider outputConsoleProvider)
            : base(LogLevel.Debug)
        {
            Assumes.Present(asyncServiceProvider);
            Assumes.Present(outputConsoleProvider);

            _asyncServiceProvider = asyncServiceProvider;
            _outputConsoleProvider = outputConsoleProvider;
        }

        public async Task StartAsync(
            RestoreOperationSource operationSource,
            Lazy<ErrorListTableDataSource> errorListDataSource,
            JoinableTaskFactory jtf,
            CancellationTokenSource cts)
        {
            Assumes.Present(errorListDataSource);
            Assumes.Present(jtf);
            Assumes.Present(cts);

            _operationSource = operationSource;
            _errorListDataSource = errorListDataSource;

            _jtc = jtf.Context.CreateCollection();
            _taskFactory = jtf.Context.CreateFactory(_jtc);

            _externalCts = cts;
            _externalCts.Token.Register(() => _cancelled = true);

            _progressFactory = t => StatusBarProgress.StartAsync(_asyncServiceProvider, _taskFactory, t);

            await _taskFactory.RunAsync(async () =>
            {
                OutputVerbosity = await GetMSBuildOutputVerbositySettingAsync();

                switch (_operationSource)
                {
                    case RestoreOperationSource.Implicit: // background auto-restore
                        _outputConsole = _outputConsoleProvider.CreatePackageManagerConsole();
                        break;
                    case RestoreOperationSource.OnBuild:
                        _outputConsole = _outputConsoleProvider.CreateBuildOutputConsole();
                        await _outputConsole.ActivateAsync();
                        break;
                    case RestoreOperationSource.Explicit:
                        _outputConsole = _outputConsoleProvider.CreatePackageManagerConsole();
                        await _outputConsole.ActivateAsync();
                        await _outputConsole.ClearAsync();
                        break;
                }
            });

            if (_errorListDataSource.IsValueCreated)
            {
                // Clear old entries
                _errorListDataSource.Value.ClearNuGetEntries();
            }
        }

        public async Task StopAsync()
        {
            await _jtc.JoinTillEmptyAsync();

            if (_showErrorList)
            {
                // Give the error list focus
                _errorListDataSource.Value.BringToFrontIfSettingsPermit();
            }
        }

        public override void LogInformationSummary(string data)
        {
            // Treat Summary as Debug
            Log(LogLevel.Debug, data);
        }

        /// <summary>
        /// Same as LogAsync but uses Do instead of DoAsync.
        /// </summary>
        public sealed override void Log(ILogMessage logMessage)
        {
            HandleErrorsAndWarnings(logMessage);

            if (DisplayMessage(logMessage.Level))
            {
                var verbosityLevel = GetMSBuildLevel(logMessage.Level);
                var reportProgress = ShouldReportProgress(logMessage);

                // Write to the output window if the verbosity level is high enough.
                var showAsOutputMessage = ShouldShowMessageAsOutput(verbosityLevel);

                // Avoid moving to the UI thread unless there is work to do
                if (reportProgress || showAsOutputMessage)
                {
                    // Make sure the message is queued in order of calls to LogAsync, but don't wait for the UI thread
                    // to actually show it.
                    _loggedMessages.Enqueue(Tuple.Create(reportProgress, showAsOutputMessage, logMessage));

                    // Run on the UI thread
                    var _ = _taskFactory.RunAsync(async () =>
                    {
                        // capture current progress from the current execution context
                        var progress = RestoreOperationProgressUI.Current;

                        await _taskFactory.SwitchToMainThreadAsync();

                        // This might be a different message than the one enqueued above, but overall the printing order
                        // will match the order of calls to LogAsync.
                        if (_loggedMessages.TryDequeue(out var message))
                        {
                            LogToVS(reportProgress: message.Item1, showAsOutputMessage: message.Item2, logMessage: message.Item3, progress: progress);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Log messages to VS. This is optimized to determine
        /// if the message needs to be logged before moving to the
        /// UI thread.
        /// </summary>
        public sealed override Task LogAsync(ILogMessage logMessage)
        {
            Log(logMessage);
            return Task.CompletedTask;
        }

        private void LogToVS(bool reportProgress, bool showAsOutputMessage, ILogMessage logMessage, RestoreOperationProgressUI progress)
        {
            var verbosityLevel = GetMSBuildLevel(logMessage.Level);

            // Progress dialog
            if (reportProgress)
            {
                progress?.ReportProgress(logMessage.Message);
            }

            // Output console
            if (showAsOutputMessage)
            {
                WriteLine(verbosityLevel, logMessage.FormatWithCode());
            }
        }

        private void HandleErrorsAndWarnings(ILogMessage logMessage)
        {
            // Display only errors/warnings
            if (logMessage.Level >= LogLevel.Warning)
            {
                var errorListEntry = new ErrorListTableEntry(logMessage);

                // Add the entry to the list
                _errorListDataSource.Value.AddNuGetEntries(errorListEntry);

                // Display the error list after restore completes
                _showErrorList = true;
            }
        }

        private static bool ShouldReportProgress(ILogMessage logMessage)
        {
            // Only show messages with VerbosityLevel.Normal. That is, info messages only.
            // Do not show errors, warnings, verbose or debug messages on the progress dialog
            // Avoid showing indented messages, these are typically not useful for the progress dialog since
            // they are missing the context of the parent text above it
            return RestoreOperationProgressUI.Current != null
                && GetMSBuildLevel(logMessage.Level) == MSBuildVerbosityLevel.Normal
                && logMessage.Message.Length == logMessage.Message.TrimStart().Length;
        }

        /// <summary>
        /// Outputs a message to the debug output pane, if the VS MSBuildOutputVerbosity
        /// setting value is greater than or equal to the given verbosity. So if verbosity is 0,
        /// it means the message is always written to the output pane.
        /// </summary>
        /// <param name="verbosity">The verbosity level.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">An array of objects to write using format. </param>
        public void WriteLine(MSBuildVerbosityLevel verbosity, string format, params object[] args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ShouldShowMessageAsOutput(verbosity))
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(() => _outputConsole.WriteLineAsync(format, args));
            }
        }

        /// <summary>
        /// True if the message has a high enough verbosity level.
        /// </summary>
        protected override bool DisplayMessage(LogLevel messageLevel)
        {
            var verbosityLevel = GetMSBuildLevel(messageLevel);

            if (_cancelled)
            {
                // If an operation is canceled, don't log anything, simply return
                // And, show a single message gets shown in the summary that package restore has been canceled
                // Do not report it as separate errors
                return false;
            }

            // If the verbosity level of message is worse than VerbosityLevel.Normal, that is,
            // VerbosityLevel.Detailed or VerbosityLevel.Diagnostic, AND,
            // _msBuildOutputVerbosity is lesser than verbosityLevel; do nothing
            if (verbosityLevel > MSBuildVerbosityLevel.Normal && OutputVerbosity < (int)verbosityLevel)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// True if this message will be written out.
        /// </summary>
        public bool ShouldShowMessageAsOutput(MSBuildVerbosityLevel verbosity)
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
                    Log(LogMessage.Create(LogLevel.Error, message));
                }
                else
                {
                    // Write to console
                    WriteLine(MSBuildVerbosityLevel.Quiet, message);
                }

                ExceptionHelper.WriteErrorToActivityLog(ex);
            });
        }

        public void ShowError(string errorText)
        {
            var entry = new ErrorListTableEntry(errorText, LogLevel.Error);

            _errorListDataSource.Value.AddNuGetEntries(entry);
            _errorListDataSource.Value.BringToFrontIfSettingsPermit();
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
                            WriteLine(MSBuildVerbosityLevel.Normal, Resources.RestoringPackages);
                            break;
                        case RestoreOperationSource.OnBuild:
                            WriteLine(MSBuildVerbosityLevel.Normal, Resources.PackageRestoreOptOutMessage);
                            break;
                        case RestoreOperationSource.Explicit:
                            WriteLine(MSBuildVerbosityLevel.Normal, Resources.RestoringPackages);
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
            var quietOrMinimal = forceStatusWrite ? MSBuildVerbosityLevel.Quiet : MSBuildVerbosityLevel.Minimal;
            var quietOrNormal = forceStatusWrite ? MSBuildVerbosityLevel.Quiet : MSBuildVerbosityLevel.Normal;
            var quietOrDetailed = forceStatusWrite ? MSBuildVerbosityLevel.Quiet : MSBuildVerbosityLevel.Detailed;

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

            await _taskFactory.SwitchToMainThreadAsync();
            action(this, progress);
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
                await _taskFactory.SwitchToMainThreadAsync();
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
        private async Task<int> GetMSBuildOutputVerbositySettingAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _asyncServiceProvider.GetDTEAsync();

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

        /// <summary>
        /// NuGet LogLevel -> MSBuild verbosity
        /// </summary>
        private static MSBuildVerbosityLevel GetMSBuildLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                case LogLevel.Warning:
                    return MSBuildVerbosityLevel.Quiet;
                case LogLevel.Minimal:
                case LogLevel.Information:
                    return MSBuildVerbosityLevel.Normal;
                case LogLevel.Verbose:
                    return MSBuildVerbosityLevel.Detailed;
                default:
                    return MSBuildVerbosityLevel.Diagnostic;
            }
        }

        /// <summary>
        /// MSBuild verbosity -> NuGet LogLevel
        /// </summary>
        private static LogLevel GetLogLevel(MSBuildVerbosityLevel level)
        {
            switch (level)
            {
                case MSBuildVerbosityLevel.Quiet:
                    return LogLevel.Warning;
                case MSBuildVerbosityLevel.Minimal:
                case MSBuildVerbosityLevel.Normal:
                    return LogLevel.Information;
                case MSBuildVerbosityLevel.Detailed:
                    return LogLevel.Verbose;
                default:
                    return LogLevel.Debug;
            }
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
                IAsyncServiceProvider asyncServiceProvider,
                JoinableTaskFactory jtf,
                CancellationToken token)
            {
                await jtf.SwitchToMainThreadAsync();

                var waitDialogFactory = await asyncServiceProvider.GetServiceAsync<
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
            }

            public override void Dispose()
            {
                _taskFactory.Run(async () =>
                {
                    await _taskFactory.SwitchToMainThreadAsync();
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
            private static object Icon = (short)Constants.SBAI_General;
            private readonly JoinableTaskFactory _taskFactory;
            private readonly IVsStatusbar _statusBar;
            private uint _cookie = 0;

            private StatusBarProgress(
                IVsStatusbar statusBar,
                JoinableTaskFactory taskFactory)
            {
                _statusBar = statusBar;
                _taskFactory = taskFactory;
            }

            public static async Task<RestoreOperationProgressUI> StartAsync(
                IAsyncServiceProvider asyncServiceProvider,
                JoinableTaskFactory jtf,
                CancellationToken token)
            {
                await jtf.SwitchToMainThreadAsync();

                var statusBar = await asyncServiceProvider.GetServiceAsync<SVsStatusbar, IVsStatusbar>();

                // Make sure the status bar is not frozen
                int frozen;
                statusBar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    statusBar.FreezeOutput(0);
                }

                statusBar.Animation(1, ref Icon);

                RestoreOperationProgressUI progress = new StatusBarProgress(statusBar, jtf);
                progress.ReportProgress(Resources.RestoringPackages);

                return progress;
            }

            public override void Dispose()
            {
                _taskFactory.Run(async () =>
                {
                    await _taskFactory.SwitchToMainThreadAsync();

                    _statusBar.Animation(0, ref Icon);
                    _statusBar.Progress(ref _cookie, 0, "", 0, 0);
                    _statusBar.FreezeOutput(0);
                    _statusBar.Clear();
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
                _statusBar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    _statusBar.FreezeOutput(0);
                }

                _statusBar.SetText(progressMessage);

                if (totalSteps != 0)
                {
                    _statusBar.Progress(ref _cookie, 1, "", currentStep, totalSteps);
                }
            }
        }
    }
}
