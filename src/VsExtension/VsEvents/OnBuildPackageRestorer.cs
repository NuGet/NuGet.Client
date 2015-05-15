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
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using Task = System.Threading.Tasks.Task;

namespace NuGetVSExtension
{
    internal sealed class OnBuildPackageRestorer
    {
        private const string LogEntrySource = "NuGet PackageRestorer";

        private readonly DTE _dte;
        private bool _outputOptOutMessage;

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

        private int TotalCount { get; set; }

        private int CurrentCount;

        private bool HasErrors { get; set; }
        private bool Canceled { get; set; }

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
            ISourceRepositoryProvider sourceRepositoryProvider)
        {
            SolutionManager = solutionManager;
            SourceRepositoryProvider = sourceRepositoryProvider;

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
                if (String.Equals(pane.Guid, buildWindowPaneGuid, StringComparison.OrdinalIgnoreCase))
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

        private void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction Action)
        {
            try
            {
                _errorListProvider.Tasks.Clear();
                PackageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;
                PackageRestoreManager.PackageRestoreFailedEvent += PackageRestoreManager_PackageRestoreFailedEvent;

                if (Action == vsBuildAction.vsBuildActionClean)
                {
                    return;
                }

                if (UsingOldPackageRestore(_dte.Solution))
                {
                    return;
                }

                if (!IsAutomatic())
                {
                    return;
                }

                _outputOptOutMessage = true;

                var solutionDirectory = SolutionManager.SolutionDirectory;

                ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        // Get the projects from the SolutionManager
                        // Note that projects that are not supported by NuGet, will not show up in this list
                        var projects = SolutionManager.GetNuGetProjects().ToList();

                        // Check if there are any projects that are not INuGetIntegratedProject, that is,
                        // projects with packages.config. If so, perform package restore on them
                        if (projects.Any(project => !(project is INuGetIntegratedProject)))
                        {
                            await RestorePackagesOrCheckForMissingPackagesAsync(solutionDirectory);
                        }

                        // Call DNU to restore for BuildIntegratedProjectSystem projects
                        var buildEnabledProjects = projects.Select(project => project as BuildIntegratedProjectSystem)
                            .Where(project => project != null);
                        if (buildEnabledProjects.Any())
                        {
                            Action<string> logMessage = message =>
                            {
                                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                                {
                                    // Switch to main thread to update the error list window or output window
                                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                    WriteLine(VerbosityLevel.Quiet, "{0}", message);
                                });
                            };

                            var context = new LoggingProjectContext(logMessage);
                            var enabledSources = SourceRepositoryProvider.GetRepositories().Select(repo => repo.PackageSource.Source);

                            // Restore packages and create the lock file for each project
                            foreach (var project in buildEnabledProjects)
                            {
                                await BuildIntegratedRestoreUtility.RestoreAsync(project, context, enabledSources, CancellationToken.None);
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                string message;
                if (_msBuildOutputVerbosity < 3)
                {
                    message = string.Format(CultureInfo.CurrentCulture, Resources.ErrorOccurredRestoringPackages, ex.Message);
                }
                else
                {
                    // output exception detail when _msBuildOutputVerbosity is >= Detailed.
                    message = string.Format(CultureInfo.CurrentCulture, Resources.ErrorOccurredRestoringPackages, ex);
                }
                WriteLine(VerbosityLevel.Quiet, message);
                ActivityLog.LogError(LogEntrySource, message);
            }
            finally
            {
                PackageRestoreManager.PackageRestoredEvent -= PackageRestoreManager_PackageRestored;
                PackageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreManager_PackageRestoreFailedEvent;
            }
        }

        /// <summary>
        /// This event could be raised from multiple threads. Only perform thread-safe operations
        /// </summary>
        private void PackageRestoreManager_PackageRestored(object sender, PackageRestoredEventArgs args)
        {
            if (Token.IsCancellationRequested)
            {
                Canceled = true;
                return;
            }

            if (args.Restored)
            {
                PackageIdentity packageIdentity = args.Package;
                Interlocked.Increment(ref CurrentCount);

                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        var progressData = new ThreadedWaitDialogProgressData(String.Format(CultureInfo.CurrentCulture, Resources.RestoredPackage, packageIdentity),
                            String.Empty, String.Empty, isCancelable: true, currentStep: CurrentCount, totalSteps: TotalCount);
                        ThreadedWaitDialogProgress.Report(progressData);
                    });
            }
        }

        private void PackageRestoreManager_PackageRestoreFailedEvent(object sender, PackageRestoreFailedEventArgs args)
        {
            if (Token.IsCancellationRequested)
            {
                // If an operation is canceled, a single message gets shown in the summary that package restore has been canceled
                // Do not report it as separate errors
                Canceled = true;
                return;
            }

            if (args.ProjectNames.Any())
            {
                // HasErrors will be used to show a message in the output window, that, Package restore failed
                // If Canceled is not already set to true
                HasErrors = true;
                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        // Switch to main thread to update the error list window or output window
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        foreach (var projectName in args.ProjectNames)
                        {
                            var exceptionMessage = _msBuildOutputVerbosity >= (int)VerbosityLevel.Detailed ?
                                args.Exception.ToString() :
                                args.Exception.Message;
                            var message = String.Format(
                                CultureInfo.CurrentCulture,
                                Resources.PackageRestoreFailedForProject, projectName,
                                exceptionMessage);

                            WriteLine(VerbosityLevel.Quiet, message);
                            ActivityLog.LogError(LogEntrySource, message);
                            MessageHelper.ShowError(_errorListProvider, TaskErrorCategory.Error,
                                TaskPriority.High, message, hierarchyItem: null);
                            WriteLine(VerbosityLevel.Normal, Resources.PackageRestoreFinishedForProject, projectName);
                        }
                    });
            }
        }

        private async Task RestorePackagesOrCheckForMissingPackagesAsync(string solutionDirectory)
        {
            // To be sure, switch to main thread before doing anything on this method
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _msBuildOutputVerbosity = GetMSBuildOutputVerbositySetting(_dte);
            var waitDialogFactory = ServiceLocator.GetGlobalService<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();

            if (String.IsNullOrEmpty(solutionDirectory))
            {
                // If the solution is closed, SolutionDirectory will be unavailable. Just return. Do nothing
                return;
            }

            var packages = await PackageRestoreManager.GetPackagesInSolutionAsync(solutionDirectory, CancellationToken.None);

            if (IsConsentGranted())
            {
                HasErrors = false;
                Canceled = false;
                CurrentCount = 0;

                if (!packages.Any())
                {
                    // Restore is not applicable, since, there is no project with installed packages, that is, packages.config
                    return;
                }

                var missingPackagesList = packages.Where(p => p.IsMissing).ToList();
                TotalCount = missingPackagesList.Count;
                if (TotalCount > 0)
                {
                    if (_outputOptOutMessage)
                    {
                        // Only show the wait dialog, when there are some packages to restore
                        using (var threadedWaitDialogSession = waitDialogFactory.StartWaitDialog(
                            waitCaption: Resources.DialogTitle,
                            initialProgress: new ThreadedWaitDialogProgressData(Resources.RestoringPackages,
                                String.Empty, String.Empty, isCancelable: true, currentStep: 0, totalSteps: 0)))
                        {
                            // Only write the PackageRestoreOptOutMessage to output window, if, there are packages to restore
                            WriteLine(VerbosityLevel.Quiet, Resources.PackageRestoreOptOutMessage);
                            _outputOptOutMessage = false;

                            Token = threadedWaitDialogSession.UserCancellationToken;
                            ThreadedWaitDialogProgress = threadedWaitDialogSession.Progress;

                            await RestoreMissingPackagesInSolutionAsync(solutionDirectory, packages, Token);

                            WriteLine(canceled: Canceled, hasMissingPackages: true, hasErrors: HasErrors);
                        }
                    }
                }
                else
                {
                    WriteLine(canceled: false, hasMissingPackages: false, hasErrors: false);
                }
            }
            else
            {
                // When the user consent is not granted, missing packages may not be restored.
                // So, we just check for them, and report them as warning(s) on the error list window

                using (var twd = waitDialogFactory.StartWaitDialog(
                    waitCaption: Resources.DialogTitle,
                    initialProgress: new ThreadedWaitDialogProgressData(Resources.RestoringPackages,
                        String.Empty, String.Empty, isCancelable: true, currentStep: 0, totalSteps: 0)))
                {
                    CheckForMissingPackages(packages);
                }
            }

            await PackageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
        }

        /// <summary>
        /// Checks if there are missing packages that should be restored. If so, a warning will
        /// be added to the error list.
        /// </summary>
        private void CheckForMissingPackages(IEnumerable<PackageRestoreData> missingPackages)
        {
            if (missingPackages.Any())
            {
                var errorText = String.Format(CultureInfo.CurrentCulture,
                    Resources.PackageNotRestoredBecauseOfNoConsent,
                    String.Join(", ", missingPackages.Select(p => p.ToString())));
                MessageHelper.ShowError(_errorListProvider, TaskErrorCategory.Error, TaskPriority.High, errorText, hierarchyItem: null);
            }
        }

        private async Task RestoreMissingPackagesInSolutionAsync(string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            await PackageRestoreManager.RestoreMissingPackagesAsync(solutionDirectory, packages, Token);
        }

        /// <summary>
        /// Returns true if the package restore user consent is granted.
        /// </summary>
        /// <returns>True if the package restore user consent is granted.</returns>
        private static bool IsConsentGranted()
        {
            var settings = ServiceLocator.GetInstance<ISettings>();
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsGranted;
        }

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        /// <returns>True if automatic package restore on build is enabled.</returns>
        private static bool IsAutomatic()
        {
            var settings = ServiceLocator.GetInstance<ISettings>();
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsAutomatic;
        }

        /// <summary>
        /// Returns true if the solution is using the old style package restore.
        /// </summary>
        /// <param name="solution">The solution to check.</param>
        /// <returns>True if the solution is using the old style package restore.</returns>
        private static bool UsingOldPackageRestore(Solution solution)
        {
            return false;
            //var nugetSolutionFolder = VsUtility.GetNuGetSolutionFolder(solution);
            //return File.Exists(Path.Combine(nugetSolutionFolder, "nuget.targets"));
        }

        private void WriteLine(bool canceled, bool hasMissingPackages, bool hasErrors)
        {
            // Write just "PackageRestore Canceled" message if package restore has been canceled
            if (canceled)
            {
                WriteLine(VerbosityLevel.Minimal, Resources.PackageRestoreCanceled);
                return;
            }

            // Write just "Nothing to restore" message when there are no missing packages.
            if (!hasMissingPackages)
            {
                WriteLine(VerbosityLevel.Minimal, Resources.NothingToRestore);
                return;
            }

            // Here package restore has happened. It can finish with/without error.
            if (hasErrors)
            {
                WriteLine(VerbosityLevel.Minimal, Resources.PackageRestoreFinishedWithError);
            }
            else
            {
                WriteLine(VerbosityLevel.Minimal, Resources.PackageRestoreFinished);
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
            var outputPane = GetBuildOutputPane();
            if (outputPane == null)
            {
                return;
            }

            if (_msBuildOutputVerbosity >= (int)verbosity)
            {
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

        /// <summary>
        /// Gets the path to .nuget folder present in the solution
        /// </summary>
        /// <param name="solution">Solution from which .nuget folder's path is obtained</param>
        private static string GetNuGetSolutionFolder(Solution solution)
        {
            Debug.Assert(solution != null);
            string solutionFilePath = (string)solution.Properties.Item("Path").Value;
            string solutionDirectory = Path.GetDirectoryName(solutionFilePath);
            return Path.Combine(solutionDirectory, NuGetVSConstants.NuGetSolutionSettingsFolder);
        }

        public void Dispose()
        {
            _errorListProvider.Dispose();
            _buildEvents.OnBuildBegin -= BuildEvents_OnBuildBegin;
            _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
        }
    }
}
