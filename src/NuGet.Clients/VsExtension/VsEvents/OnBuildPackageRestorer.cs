// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using Task = System.Threading.Tasks.Task;

namespace NuGetVSExtension
{
    internal class OnBuildPackageRestorer : ILogger
    {
        private const string LogEntrySource = "NuGet PackageRestorer";

        private readonly DTE _dte;

        private Dictionary<string, BuildIntegratedProjectCacheEntry> _buildIntegratedCache
            = new Dictionary<string, BuildIntegratedProjectCacheEntry>(StringComparer.Ordinal);

        // The value of the "MSBuild project build output verbosity" setting 
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        private int _msBuildOutputVerbosity;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected.
        private readonly BuildEvents _buildEvents;

        private readonly SolutionEvents _solutionEvents;

        private readonly ErrorListProvider _errorListProvider;

        private IProgress<ThreadedWaitDialogProgressData> ThreadedWaitDialogProgress { get; set; }

        private CancellationToken Token { get; set; }

        private IPackageRestoreManager PackageRestoreManager { get; }

        private ISolutionManager SolutionManager { get; }

        private ISourceRepositoryProvider SourceRepositoryProvider { get; }

        private ISettings Settings { get; }

        private INuGetProjectContext NuGetProjectContext { get; }

        private int TotalCount { get; set; }

        private int CurrentCount;

        // Restore summary
        // True if any of restores had errors
        private bool _hasErrors;
        // True if any of the restores were canceled
        private bool _canceled;
        // True if any restores failed to restore all packages
        private bool _hasMissingPackages;
        // True if restore actions were taken and the summary should be displayed
        private bool _displayRestoreSummary;
        // If false the opt out message should be displayed
        private bool _hasOptOutBeenShown;

        private enum VerbosityLevel
        {
            Quiet = 0,
            Minimal = 1,
            Normal = 2,
            Detailed = 3,
            Diagnostic = 4
        };

        internal OnBuildPackageRestorer(ISolutionManager solutionManager,
            IPackageRestoreManager packageRestoreManager,
            IServiceProvider serviceProvider,
            ISourceRepositoryProvider sourceRepositoryProvider,
            ISettings settings,
            INuGetProjectContext nuGetProjectContext)
        {
            SolutionManager = solutionManager;
            SourceRepositoryProvider = sourceRepositoryProvider;
            Settings = settings;
            NuGetProjectContext = nuGetProjectContext;

            PackageRestoreManager = packageRestoreManager;

            _dte = ServiceLocator.GetInstance<DTE>();
            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            _errorListProvider = new ErrorListProvider(serviceProvider);
        }

        private OutputWindowPane GetBuildOutputPane()
        {
            // get the "Build" output window pane
            var dte2 = (DTE2)_dte;
            var buildWindowPaneGuid = VSConstants.BuildOutput.ToString("B");
            foreach (OutputWindowPane pane in dte2.ToolWindows.OutputWindow.OutputWindowPanes)
            {
                if (string.Equals(pane.Guid, buildWindowPaneGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return pane;
                }
            }

            return null;
        }

        private void SolutionEvents_AfterClosing()
        {
            _errorListProvider.Tasks.Clear();
        }

        /// <summary>
        /// RestorePackages is triggered by the menu item.  It performs the same
        /// actions as a build.
        /// </summary>
        internal void RestorePackages()
        {
            try
            {
                // Execute
                Restore(forceRestore: true, showOptOutMessage: false);
            }
            catch (Exception ex)
            {
                // Log the exception to the console and activity log
                LogException(ex, logError: true);
            }

            // Always write out the final status message, even if no actions took place.
            WriteLine(_canceled, _hasMissingPackages, _hasErrors, forceStatusWrite: true);
        }

        private void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction Action)
        {
            try
            {
                if (Action == vsBuildAction.vsBuildActionClean)
                {
                    // Clear the project.json restore cache on clean to ensure that the next build restores again
                    if (_buildIntegratedCache != null)
                    {
                        _buildIntegratedCache.Clear();
                    }

                    return;
                }

                if (!IsAutomatic(Settings))
                {
                    return;
                }

                var forceRestore = Action == vsBuildAction.vsBuildActionRebuildAll;

                // Execute
                Restore(forceRestore, showOptOutMessage: true);
            }
            catch (Exception ex)
            {
                // Log the exception to the console and activity log
                LogException(ex, logError: false);
            }

            if (_displayRestoreSummary)
            {
                // If actions were performed, display the summary
                WriteLine(_canceled, _hasMissingPackages, _hasErrors, forceStatusWrite: false);
            }
        }

        private void Restore(bool forceRestore, bool showOptOutMessage)
        {
            // Reset flags for the final status messsage
            _hasErrors = false;
            _canceled = false;
            _hasMissingPackages = false;
            _displayRestoreSummary = false;
            _hasOptOutBeenShown = !showOptOutMessage;

            try
            {
                _errorListProvider.Tasks.Clear();
                PackageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;
                PackageRestoreManager.PackageRestoreFailedEvent += PackageRestoreManager_PackageRestoreFailedEvent;

                var solutionDirectory = SolutionManager.SolutionDirectory;
                var isSolutionAvailable = SolutionManager.IsSolutionAvailable;

                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    // Get MSBuildOutputVerbosity from _dte
                    _msBuildOutputVerbosity = GetMSBuildOutputVerbositySetting(_dte);

                    // Get the projects from the SolutionManager
                    // Note that projects that are not supported by NuGet, will not show up in this list
                    var projects = SolutionManager.GetNuGetProjects().ToList();

                    // Check if there are any projects that are not INuGetIntegratedProject, that is,
                    // projects with packages.config. If so, perform package restore on them
                    if (projects.Any(project => !(project is INuGetIntegratedProject)))
                    {
                        await RestorePackagesOrCheckForMissingPackagesAsync(
                            solutionDirectory,
                            isSolutionAvailable);
                    }

                    // Call DNU to restore for BuildIntegratedProjectSystem projects
                    var buildEnabledProjects = projects.OfType<BuildIntegratedProjectSystem>();

                    await RestoreBuildIntegratedProjectsAsync(
                        solutionDirectory,
                        buildEnabledProjects.ToList(),
                        forceRestore,
                        isSolutionAvailable);

                }, JoinableTaskCreationOptions.LongRunning);
            }
            finally
            {
                PackageRestoreManager.PackageRestoredEvent -= PackageRestoreManager_PackageRestored;
                PackageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreManager_PackageRestoreFailedEvent;
            }
        }

        private void LogException(Exception ex, bool logError)
        {
            string message;
            if (_msBuildOutputVerbosity < 3)
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

        private void DisplayOptOutMessage()
        {
            if (!_hasOptOutBeenShown)
            {
                _hasOptOutBeenShown = true;

                // Only write the PackageRestoreOptOutMessage to output window,
                // if, there are packages to restore
                WriteLine(VerbosityLevel.Quiet, Resources.PackageRestoreOptOutMessage);
            }
        }

        /// <summary>
        /// Restore projects with project.json and create the lock files.
        /// </summary>
        /// <param name="buildEnabledProjects">Projects containing project.json</param>
        /// <param name="forceRestore">Force the restore to write out the lock files.
        /// This is used for rebuilds.</param>
        /// <returns></returns>
        private async Task RestoreBuildIntegratedProjectsAsync(
            string solutionDirectory,
            List<BuildIntegratedProjectSystem> buildEnabledProjects,
            bool forceRestore,
            bool isSolutionAvailable)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (buildEnabledProjects.Any() && IsConsentGranted(Settings))
            {
                if (!isSolutionAvailable)
                {
                    var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings);
                    if (!Path.IsPathRooted(globalPackagesFolder))
                    {
                        var message = string.Format(
                            CultureInfo.CurrentCulture,
                            NuGet.PackageManagement.VisualStudio.Strings.RelativeGlobalPackagesFolder,
                            globalPackagesFolder);

                        WriteLine(VerbosityLevel.Quiet, message);

                        // Cannot restore packages since globalPackagesFolder is a relative path
                        // and the solution is not available
                        return;
                    }
                }

                var enabledSources = SourceRepositoryProvider.GetRepositories();

                // No-op all project closures are up to date and all packages exist on disk.
                if (await IsRestoreRequired(buildEnabledProjects, forceRestore))
                {
                    var waitDialogFactory
                        = ServiceLocator.GetGlobalService<SVsThreadedWaitDialogFactory,
                            IVsThreadedWaitDialogFactory>();

                    // NOTE: During restore for build integrated projects,
                    //       We might show the dialog even if there are no packages to restore
                    // When both currentStep and totalSteps are 0, we get a marquee on the dialog
                    using (var threadedWaitDialogSession = waitDialogFactory.StartWaitDialog(
                        waitCaption: Resources.DialogTitle,
                        initialProgress: new ThreadedWaitDialogProgressData(Resources.RestoringPackages,
                            string.Empty,
                            string.Empty,
                            isCancelable: true,
                            currentStep: 0,
                            totalSteps: 0)))
                    {
                        // Display the restore opt out message if it has not been shown yet
                        DisplayOptOutMessage();

                        Token = threadedWaitDialogSession.UserCancellationToken;
                        ThreadedWaitDialogProgress = threadedWaitDialogSession.Progress;

                        // Restore packages and create the lock file for each project
                        foreach (var project in buildEnabledProjects)
                        {
                            // Mark this as having missing packages so that we will not 
                            // display a noop message in the summary
                            _hasMissingPackages = true;
                            _displayRestoreSummary = true;

                            // Skip further restores if the user has clicked cancel
                            if (!Token.IsCancellationRequested)
                            {
                                var projectName = NuGetProject.GetUniqueNameOrName(project);

                                try
                                {
                                    // Restore and create a project.lock.json file
                                    await BuildIntegratedProjectRestoreAsync(
                                        project,
                                        solutionDirectory,
                                        enabledSources,
                                        Token);
                                }
                                catch (Exception ex)
                                {
                                    // Allow other projects to be restored when a single project fails.
                                    // If an unexpected exception occurs from the RestoreCommand
                                    // log it to the console and error window.
                                    LogException(ex, logError: true);
                                    _hasErrors = true;
                                }

                                WriteLine(
                                    VerbosityLevel.Normal,
                                    Resources.PackageRestoreFinishedForProject,
                                    projectName);
                            }
                        }

                        if (Token.IsCancellationRequested)
                        {
                            _canceled = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determine if a full restore is required. For scenarios with no floating versions
        /// where all packages are already on disk and no server requests are needed
        /// we can skip the restore after some validation checks.
        /// </summary>
        private async Task<bool> IsRestoreRequired(List<BuildIntegratedProjectSystem> projects, bool forceRestore)
        {
            try
            {
                // Swap caches 
                var oldCache = _buildIntegratedCache;
                _buildIntegratedCache
                    = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

                if (forceRestore)
                {
                    // The cache has been updated, now skip the check since we are doing a restore anyways.
                    return true;
                }

                if (BuildIntegratedRestoreUtility.CacheHasChanges(oldCache, _buildIntegratedCache))
                {
                    // A new project has been added
                    return true;
                }

                var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings);
                var pathResolver = new VersionFolderPathResolver(globalPackagesFolder);

                if (BuildIntegratedRestoreUtility.IsRestoreRequired(projects, pathResolver))
                {
                    // The project.json file does not match the lock file
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.Fail("Unable to validate lock files.");

                ActivityLog.LogError(LogEntrySource, ex.ToString());

                WriteLine(VerbosityLevel.Diagnostic, "{0}", ex.ToString());

                // If we are unable to validate, run a full restore
                // This may occur if the lock files are corrupt
                return true;
            }

            // Validation passed, no restore is needed
            return false;
        }

        private async Task BuildIntegratedProjectRestoreAsync(
            BuildIntegratedNuGetProject project,
            string solutionDirectory,
            IEnumerable<SourceRepository> enabledSources,
            CancellationToken token)
        {
            // Go off the UI thread to perform I/O operations
            await TaskScheduler.Default;

            var projectName = NuGetProject.GetUniqueNameOrName(project);

            var effectiveGlobalPackagesFolder = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                                                    SolutionManager?.SolutionDirectory,
                                                    Settings);

            // Pass down the CancellationToken from the dialog
            var restoreResult = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                this,
                enabledSources,
                effectiveGlobalPackagesFolder,
                token);

            if (!restoreResult.Success)
            {
                // Mark this as having errors
                _hasErrors = true;

                // Invalidate cached results for the project. This will cause it to restore the next time.
                _buildIntegratedCache.Remove(projectName);
                await BuildIntegratedProjectReportErrorAsync(projectName, restoreResult, token);
            }
        }

        private async Task BuildIntegratedProjectReportErrorAsync(string projectName,
            RestoreResult restoreResult,
            CancellationToken token)
        {
            Debug.Assert(!restoreResult.Success);

            if (token.IsCancellationRequested)
            {
                // If an operation is canceled, a single message gets shown in the summary
                // that package restore has been canceled. Do not report it as separate errors
                _canceled = true;
                return;
            }

            // HasErrors will be used to show a message in the output window, that, Package restore failed
            // If Canceled is not already set to true
            _hasErrors = true;

            // Switch to main thread to update the error list window or output window
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var libraryRange in restoreResult.GetAllUnresolved())
            {
                var message = string.Format(CultureInfo.CurrentCulture,
                    Resources.BuildIntegratedPackageRestoreFailedForProject,
                    projectName,
                    libraryRange.ToString());

                WriteLine(VerbosityLevel.Quiet, message);

                MessageHelper.ShowError(_errorListProvider,
                    TaskErrorCategory.Error,
                    TaskPriority.High,
                    message,
                    hierarchyItem: null);
            }
        }

        /// <summary>
        /// This event could be raised from multiple threads. Only perform thread-safe operations
        /// </summary>
        private void PackageRestoreManager_PackageRestored(object sender, PackageRestoredEventArgs args)
        {
            if (Token.IsCancellationRequested)
            {
                _canceled = true;
                return;
            }

            if (args.Restored)
            {
                var packageIdentity = args.Package;
                Interlocked.Increment(ref CurrentCount);

                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var progressData = new ThreadedWaitDialogProgressData(string.Format(CultureInfo.CurrentCulture,
                            Resources.RestoredPackage,
                            packageIdentity),
                        string.Empty,
                        string.Empty,
                        isCancelable: true,
                        currentStep: CurrentCount,
                        totalSteps: TotalCount);
                    ThreadedWaitDialogProgress.Report(progressData);
                });
            }
        }

        private void PackageRestoreManager_PackageRestoreFailedEvent(
            object sender,
            PackageRestoreFailedEventArgs args)
        {
            if (Token.IsCancellationRequested)
            {
                // If an operation is canceled, a single message gets shown in the summary
                // that package restore has been canceled
                // Do not report it as separate errors
                _canceled = true;
                return;
            }

            if (args.ProjectNames.Any())
            {
                // HasErrors will be used to show a message in the output window, that, Package restore failed
                // If Canceled is not already set to true
                _hasErrors = true;
                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        // Switch to main thread to update the error list window or output window
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        foreach (var projectName in args.ProjectNames)
                        {
                            var exceptionMessage = _msBuildOutputVerbosity >= (int)VerbosityLevel.Detailed ?
                                args.Exception.ToString() :
                                args.Exception.Message;
                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                Resources.PackageRestoreFailedForProject, projectName,
                                exceptionMessage);

                            WriteLine(VerbosityLevel.Quiet, message);

                            MessageHelper.ShowError(_errorListProvider, TaskErrorCategory.Error,
                                TaskPriority.High, message, hierarchyItem: null);
                            WriteLine(VerbosityLevel.Normal, Resources.PackageRestoreFinishedForProject, projectName);
                        }
                    });
            }
        }

        private async Task RestorePackagesOrCheckForMissingPackagesAsync(
            string solutionDirectory,
            bool isSolutionAvailable)
        {
            // To be sure, switch to main thread before doing anything on this method
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var waitDialogFactory
                = ServiceLocator.GetGlobalService<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

            if (string.IsNullOrEmpty(solutionDirectory))
            {
                // If the solution is closed, SolutionDirectory will be unavailable. Just return. Do nothing
                return;
            }

            var packages = await PackageRestoreManager.GetPackagesInSolutionAsync(solutionDirectory,
                CancellationToken.None);

            if (IsConsentGranted(Settings))
            {
                CurrentCount = 0;

                if (!packages.Any())
                {
                    if (!isSolutionAvailable)
                    {
                        MessageHelper.ShowError(_errorListProvider,
                            TaskErrorCategory.Error,
                            TaskPriority.High,
                            NuGet.PackageManagement.VisualStudio.Strings.SolutionIsNotSaved,
                            hierarchyItem: null);

                        WriteLine(
                            VerbosityLevel.Quiet,
                            NuGet.PackageManagement.VisualStudio.Strings.SolutionIsNotSaved);
                    }

                    // Restore is not applicable, since, there is no project with installed packages
                    return;
                }

                var missingPackagesList = packages.Where(p => p.IsMissing).ToList();
                TotalCount = missingPackagesList.Count;
                if (TotalCount > 0)
                {
                    // Only show the wait dialog, when there are some packages to restore
                    using (var threadedWaitDialogSession = waitDialogFactory.StartWaitDialog(
                        waitCaption: Resources.DialogTitle,
                        initialProgress: new ThreadedWaitDialogProgressData(
                            Resources.RestoringPackages,
                            string.Empty,
                            string.Empty,
                            isCancelable: true,
                            currentStep: 0,
                            totalSteps: 0)))
                    {
                        Token = threadedWaitDialogSession.UserCancellationToken;
                        ThreadedWaitDialogProgress = threadedWaitDialogSession.Progress;

                        // Display the restore opt out message if it has not been shown yet
                        DisplayOptOutMessage();

                        await RestoreMissingPackagesInSolutionAsync(solutionDirectory, packages, Token);

                        // Mark that work is being done during this restore
                        _hasMissingPackages = true;
                        _displayRestoreSummary = true;
                    }
                }
            }
            else
            {
                // When the user consent is not granted, missing packages may not be restored.
                // So, we just check for them, and report them as warning(s) on the error list window

                using (var twd = waitDialogFactory.StartWaitDialog(
                    waitCaption: Resources.DialogTitle,
                    initialProgress: new ThreadedWaitDialogProgressData(
                        Resources.RestoringPackages,
                        string.Empty,
                        string.Empty,
                        isCancelable: true,
                        currentStep: 0,
                        totalSteps: 0)))
                {
                    CheckForMissingPackages(packages);
                }
            }

            await PackageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory,
                CancellationToken.None);
        }

        /// <summary>
        /// Checks if there are missing packages that should be restored. If so, a warning will
        /// be added to the error list.
        /// </summary>
        private void CheckForMissingPackages(IEnumerable<PackageRestoreData> missingPackages)
        {
            if (missingPackages.Any())
            {
                var errorText = string.Format(CultureInfo.CurrentCulture,
                    Resources.PackageNotRestoredBecauseOfNoConsent,
                    string.Join(", ", missingPackages.Select(p => p.ToString())));
                MessageHelper.ShowError(_errorListProvider,
                    TaskErrorCategory.Error,
                    TaskPriority.High,
                    errorText,
                    hierarchyItem: null);
            }
        }

        private async Task RestoreMissingPackagesInSolutionAsync(string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            await PackageRestoreManager.RestoreMissingPackagesAsync(solutionDirectory,
                packages,
                NuGetProjectContext,
                token);
        }

        /// <summary>
        /// Returns true if the package restore user consent is granted.
        /// </summary>
        /// <returns>True if the package restore user consent is granted.</returns>
        private static bool IsConsentGranted(ISettings settings)
        {
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsGranted;
        }

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        /// <returns>True if automatic package restore on build is enabled.</returns>
        private static bool IsAutomatic(ISettings settings)
        {
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsAutomatic;
        }

        private void WriteLine(bool canceled, bool hasMissingPackages, bool hasErrors, bool forceStatusWrite)
        {
            // Write just "PackageRestore Canceled" message if package restore has been canceled
            if (canceled)
            {
                WriteLine(
                    forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Minimal,
                    Resources.PackageRestoreCanceled);

                return;
            }

            // Write just "Nothing to restore" message when there are no missing packages.
            if (!hasMissingPackages)
            {
                WriteLine(
                    forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Detailed,
                    Resources.NothingToRestore);

                return;
            }

            // Here package restore has happened. It can finish with/without error.
            if (hasErrors)
            {
                WriteLine(
                    forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Minimal,
                    Resources.PackageRestoreFinishedWithError);
            }
            else
            {
                WriteLine(
                    forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Normal,
                    Resources.PackageRestoreFinished);
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
        private void WriteLine(VerbosityLevel verbosity, string format, params object[] args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_msBuildOutputVerbosity >= (int)verbosity)
            {
                var outputPane = GetBuildOutputPane();
                if (outputPane == null)
                {
                    return;
                }

                var msg = string.Format(CultureInfo.CurrentCulture, format, args);
                outputPane.OutputString(msg);
                outputPane.OutputString(Environment.NewLine);
            }
        }

        /// <summary>
        /// Returns the value of the VisualStudio MSBuildOutputVerbosity setting.
        /// </summary>
        /// <param name="dte">The VisualStudio instance.</param>
        /// <remarks>
        /// 0 is Quiet, while 4 is diagnostic.
        /// </remarks>
        private static int GetMSBuildOutputVerbositySetting(DTE dte)
        {
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
            _errorListProvider.Dispose();
            _buildEvents.OnBuildBegin -= BuildEvents_OnBuildBegin;
            _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
        }

        #region ILogger implementation
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

        public void LogWarning(string data)
        {
            LogToVS(VerbosityLevel.Minimal, data);
        }

        public void LogError(string data)
        {
            LogToVS(VerbosityLevel.Quiet, data);
        }
        #endregion ILogger implementation

        private void LogToVS(VerbosityLevel verbosityLevel, string message)
        {
            if (Token.IsCancellationRequested)
            {
                // If an operation is canceled, don't log anything, simply return
                // And, show a single message gets shown in the summary that package restore has been canceled
                // Do not report it as separate errors
                _canceled = true;
                return;
            }

            // If the verbosity level of message is worse than VerbosityLevel.Normal, that is,
            // VerbosityLevel.Detailed or VerbosityLevel.Diagnostic, AND,
            // _msBuildOutputVerbosity is lesser than verbosityLevel; do nothing
            if (verbosityLevel > VerbosityLevel.Normal && _msBuildOutputVerbosity < (int)verbosityLevel)
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
                    ThreadedWaitDialogProgress.Report(progressData);
                }

                // Write to the output window. Based on _msBuildOutputVerbosity, the message may or may not
                // get shown on the output window. Default is VerbosityLevel.Minimal
                WriteLine(verbosityLevel, message);

                // VerbosityLevel.Quiet corresponds to ILogger.LogError, and,
                // VerbosityLevel.Minimal corresponds to ILogger.LogWarning
                // In these 2 cases, we add an error or warning to the error list window
                if (verbosityLevel == VerbosityLevel.Quiet || verbosityLevel == VerbosityLevel.Minimal)
                {
                    MessageHelper.ShowError(_errorListProvider,
                        verbosityLevel == VerbosityLevel.Quiet ? TaskErrorCategory.Error : TaskErrorCategory.Warning,
                        TaskPriority.High,
                        message,
                        hierarchyItem: null);
                }
            });
        }
    }
}
