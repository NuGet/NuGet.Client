// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Aggregates logging and UI services consumed by the <see cref="SolutionRestoreJob"/>.
    /// </summary>
    internal sealed class RestoreOperationLogger : ILogger, IDisposable
    {
        private const string LogEntrySource = "NuGet PackageRestorer";
        private static readonly string BuildWindowPaneGuid = VSConstants.BuildOutput.ToString("B");

        private readonly IServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private readonly Lazy<EnvDTE.OutputWindowPane> _buildOutputPane;

        private bool _cancelled;

        // The value of the "MSBuild project build output verbosity" setting
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        private readonly Lazy<int> _msbuildOutputVerbosity;

        public int OutputVerbosity => _msbuildOutputVerbosity.Value;

        public void SetCancelled() => _cancelled = true;

        public RestoreOperationLogger(
            IServiceProvider serviceProvider,
            ErrorListProvider errorListProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (errorListProvider == null)
            {
                throw new ArgumentNullException(nameof(errorListProvider));
            }

            _serviceProvider = serviceProvider;
            _errorListProvider = errorListProvider;

            var dte = serviceProvider.GetDTE();

            _msbuildOutputVerbosity = new Lazy<int>(
                valueFactory: () => GetMSBuildOutputVerbositySetting(dte));

            _buildOutputPane = new Lazy<EnvDTE.OutputWindowPane>(
                valueFactory: () => GetBuildOutputPane(dte));
        }

        public void Dispose()
        {
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

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                // Switch to main thread to update the progress dialog, output window or error list window
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Only show messages with VerbosityLevel.Normal. That is, info messages only.
                // Do not show errors, warnings, verbose or debug messages on the progress dialog
                // Avoid showing indented messages, these are typically not useful for the progress dialog since
                // they are missing the context of the parent text above it
                if (verbosityLevel == VerbosityLevel.Normal &&
                    message.Length == message.TrimStart().Length)
                {
                    await RestoreOperationProgressUI.Current.ReportProgressAsync(message);
                }

                // Write to the output window. Based on _msBuildOutputVerbosity, the message may or may not
                // get shown on the output window. Default is VerbosityLevel.Minimal
                WriteLine(verbosityLevel, message);

                // VerbosityLevel.Quiet corresponds to ILogger.LogError, and,
                // VerbosityLevel.Minimal corresponds to ILogger.LogWarning
                // In these 2 cases, we add an error or warning to the error list window
                if (verbosityLevel == VerbosityLevel.Quiet ||
                    verbosityLevel == VerbosityLevel.Minimal)
                {
                    MessageHelper.ShowError(
                        _errorListProvider,
                        verbosityLevel == VerbosityLevel.Quiet ? TaskErrorCategory.Error : TaskErrorCategory.Warning,
                        TaskPriority.High,
                        message,
                        hierarchyItem: null);
                }
            });
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

            if (OutputVerbosity >= (int)verbosity && _buildOutputPane.Value != null)
            {
                var outputPane = _buildOutputPane.Value;

                var msg = string.Format(CultureInfo.CurrentCulture, format, args);
                outputPane.OutputString(msg);
                outputPane.OutputString(Environment.NewLine);
            }
        }

        private static EnvDTE.OutputWindowPane GetBuildOutputPane(EnvDTE.DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte2 = (DTE2)dte;
            var pane = dte2.ToolWindows.OutputWindow
                .OutputWindowPanes
                .Cast<EnvDTE.OutputWindowPane>()
                .FirstOrDefault(p => StringComparer.OrdinalIgnoreCase.Equals(p.Guid, BuildWindowPaneGuid));
            return pane;
        }

        public void LogException(Exception ex, bool logError)
        {
            string message;
            if (OutputVerbosity < 3)
            {
                message = string.Format(CultureInfo.CurrentCulture,
                    Strings.ErrorOccurredRestoringPackages,
                    ex.Message);
            }
            else
            {
                // output exception detail when _msBuildOutputVerbosity is >= Detailed.
                message = string.Format(CultureInfo.CurrentCulture, Strings.ErrorOccurredRestoringPackages, ex);
            }

            if (logError)
            {
                // Write to the error window and console
                LogError(message);
            }
            else
            {
                // Write to console
                WriteLine(VerbosityLevel.Quiet, message);
            }

            ActivityLog.LogError(LogEntrySource, message);
        }

        public void ShowError(string errorText)
        {
            MessageHelper.ShowError(
                _errorListProvider,
                TaskErrorCategory.Error,
                TaskPriority.High,
                errorText,
                hierarchyItem: null);
        }

        /// <summary>
        /// Returns the value of the VisualStudio MSBuildOutputVerbosity setting.
        /// </summary>
        /// <param name="dte">The VisualStudio instance.</param>
        /// <remarks>
        /// 0 is Quiet, while 4 is diagnostic.
        /// </remarks>
        private static int GetMSBuildOutputVerbositySetting(EnvDTE.DTE dte)
        {
            var properties = dte.get_Properties("Environment", "ProjectsAndSolution");
            var value = properties.Item("MSBuildOutputVerbosity").Value;
            if (value is int)
            {
                return (int)value;
            }
            return 0;
        }

        public Task<RestoreOperationProgressUI> StartProgressSessionAsync(CancellationToken token)
        {
            return Task.FromResult(WaitDialogProgress.Start(_serviceProvider));
                // StatusBarProgress.StartAsync(_serviceProvider, token);
        }

        private class WaitDialogProgress : RestoreOperationProgressUI
        {
            private readonly ThreadedWaitDialogHelper.Session _session;

            private WaitDialogProgress(ThreadedWaitDialogHelper.Session session)
            {
                _session = session;
                UserCancellationToken = _session.UserCancellationToken;
            }

            public static RestoreOperationProgressUI Start(IServiceProvider serviceProvider)
            {
                var waitDialogFactory = serviceProvider.GetService<
                    SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

                var session = waitDialogFactory.StartWaitDialog(
                    waitCaption: Strings.DialogTitle,
                    initialProgress: new ThreadedWaitDialogProgressData(
                        Strings.RestoringPackages,
                        progressText: string.Empty,
                        statusBarText: string.Empty,
                        isCancelable: true,
                        currentStep: 0,
                        totalSteps: 0));

                var progress = new WaitDialogProgress(session);
                _instance.Value = progress;

                return progress;
            }

            public override void Dispose()
            {
                _instance.Value = null;
                _session.Dispose();
            }

            public override async Task ReportProgressAsync(
                string progressMessage,
                uint currentStep,
                uint totalSteps)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
            private readonly IVsStatusbar StatusBar;
            private uint cookie = 0;

            private StatusBarProgress(IVsStatusbar statusBar)
            {
                StatusBar = statusBar;
            }

            public static async Task<RestoreOperationProgressUI> StartAsync(
                IServiceProvider serviceProvider,
                CancellationToken token)
            {
                var StatusBar = serviceProvider.GetService<SVsStatusbar, IVsStatusbar>();

                // Make sure the status bar is not frozen
                int frozen;
                StatusBar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    StatusBar.FreezeOutput(0);
                }

                StatusBar.Animation(1, ref icon);

                RestoreOperationProgressUI progress = new StatusBarProgress(StatusBar);
                await progress.ReportProgressAsync(Strings.RestoringPackages);

                return _instance.Value = progress;
            }

            public override void Dispose()
            {
                _instance.Value = null;

                StatusBar.Animation(0, ref icon);
                StatusBar.Progress(ref cookie, 0, "", 0, 0);
                StatusBar.FreezeOutput(0);
                StatusBar.Clear();
            }

            public override async Task ReportProgressAsync(
                string progressMessage,
                uint currentStep,
                uint totalSteps)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Make sure the status bar is not frozen
                int frozen;
                StatusBar.IsFrozen(out frozen);

                if (frozen != 0)
                {
                    StatusBar.FreezeOutput(0);
                }

                StatusBar.SetText(progressMessage);
                StatusBar.Progress(ref cookie, 1, "", currentStep, totalSteps);
            }
        }
    }
}
