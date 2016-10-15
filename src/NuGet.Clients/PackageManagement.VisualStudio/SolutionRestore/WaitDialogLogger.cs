// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Aggregates logging and UI services consumed by the <see cref="SolutionRestoreJob"/>.
    /// </summary>
    internal sealed class WaitDialogLogger : ILogger, IDisposable
    {
        private const string LogEntrySource = "NuGet PackageRestorer";
        private static readonly string BuildWindowPaneGuid = VSConstants.BuildOutput.ToString("B");

        private readonly IVsThreadedWaitDialogFactory _waitDialogFactory;
        private readonly ErrorListProvider _errorListProvider;

        private IProgress<ThreadedWaitDialogProgressData> _progress;
        private bool _cancelled;

        private readonly Lazy<EnvDTE.OutputWindowPane> _buildOutputPane;

        // The value of the "MSBuild project build output verbosity" setting 
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        private readonly Lazy<int> _msbuildOutputVerbosity;

        public int OutputVerbosity => _msbuildOutputVerbosity.Value;

        public void SetCancelled() => _cancelled = true;

        public WaitDialogLogger(
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

            _waitDialogFactory = serviceProvider.GetService<
                SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

            _errorListProvider = errorListProvider;
            _errorListProvider.Tasks.Clear();

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
                if (verbosityLevel == VerbosityLevel.Normal && message.Length == message.TrimStart().Length)
                {
                    // When both currentStep and totalSteps are 0, we get a marquee on the dialog
                    var progressData = new ThreadedWaitDialogProgressData(message,
                        string.Empty,
                        string.Empty,
                        isCancelable: true,
                        currentStep: 0,
                        totalSteps: 0);

                    // Update the progress dialog
                    _progress.Report(progressData);
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

        public ThreadedWaitDialogHelper.Session StartWaitDialog()
        {
            var session = _waitDialogFactory.StartWaitDialog(
                waitCaption: Strings.DialogTitle,
                initialProgress: new ThreadedWaitDialogProgressData(
                    Strings.RestoringPackages,
                    progressText: string.Empty,
                    statusBarText: string.Empty,
                    isCancelable: true,
                    currentStep: 0,
                    totalSteps: 0));

            _progress = session.Progress;

            return session;
        }

        public async Task ReportProgressAsync(
            string progressMessage, int currentStep, int totalSteps)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var progressData = new ThreadedWaitDialogProgressData(
                progressMessage,
                progressText: string.Empty,
                statusBarText: string.Empty,
                isCancelable: true,
                currentStep: currentStep,
                totalSteps: totalSteps);

            _progress.Report(progressData);
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
    }
}
