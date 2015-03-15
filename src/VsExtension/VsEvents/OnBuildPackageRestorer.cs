using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    internal sealed class OnBuildPackageRestorer
    {
        private const string LogEntrySource = "NuGet PackageRestorer";

        private DTE _dte;
        private bool _outputOptOutMessage;

        // The value of the "MSBuild project build output verbosity" setting 
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        private int _msBuildOutputVerbosity;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected.
        private BuildEvents _buildEvents;

        private SolutionEvents _solutionEvents;

        private ErrorListProvider _errorListProvider;

        private IVsThreadedWaitDialog2 _waitDialog;

        private CancellationTokenSource CancellationTokenSource { get; set; }

        private IPackageRestoreManager PackageRestoreManager { get; set; }

        private ISolutionManager SolutionManager { get; set; }

        private int TotalCount { get; set; }

        private int CurrentCount;

        private int WaitDialogUpdateGate = 0;

        enum VerbosityLevel
        {
            Quiet = 0,
            Minimal = 1,
            Normal = 2,
            Detailed = 3,
            Diagnostic = 4
        };

        internal OnBuildPackageRestorer(ISolutionManager solutionManager, IPackageRestoreManager packageRestoreManager, IServiceProvider serviceProvider)
        {
            SolutionManager = solutionManager;

            PackageRestoreManager = packageRestoreManager;

            _dte = ServiceLocator.GetInstance<DTE>();
            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            _errorListProvider = new ErrorListProvider(serviceProvider);

            // Create a non-null but cancelled CancellationTokenSource
            CancellationTokenSource = new CancellationTokenSource();
            CancellationTokenSource.Cancel();
        }

        OutputWindowPane GetBuildOutputPane()
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

        private async void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction Action)
        {
            CancellationTokenSource = new CancellationTokenSource();
            try
            {
                _errorListProvider.Tasks.Clear();
                PackageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;

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
                await RestorePackagesOrCheckForMissingPackages(scope);
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
                    message = string.Format(CultureInfo.CurrentCulture, Resources.ErrorOccurredRestoringPackages, ex.ToString());
                }
                WriteLine(VerbosityLevel.Quiet, message);
                ActivityLog.LogError(LogEntrySource, message);
            }
            finally
            {
                PackageRestoreManager.PackageRestoredEvent -= PackageRestoreManager_PackageRestored;
                CancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// This event could be raised from multiple threads. Only perform thread-safe operations
        /// </summary>
        private void PackageRestoreManager_PackageRestored(object sender, PackageRestoredEventArgs args)
        {
            PackageIdentity packageIdentity = args.Package;
            if (args.Restored && CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
            {
                bool canceled = false;
                Interlocked.Increment(ref CurrentCount);
                
                // The rate at which the packages are restored is much higher than the rate at which a wait dialog can be updated
                // And, this event is raised by multiple threads
                // So, only try to update the wait dialog if an update is not already in progress. Use the int 'WaitDialogUpdateGate' for this purpose
                // Always, set it to 1 below and gets its old value. If the old value is 0, go and update, otherwise, bail
                if (Interlocked.Equals(Interlocked.Exchange(ref WaitDialogUpdateGate, 1), 0))
                {
                    _waitDialog.UpdateProgress(
                        String.Format(CultureInfo.CurrentCulture, Resources.RestoredPackage, packageIdentity.ToString()),
                        String.Empty,
                        szStatusBarText: null,
                        iCurrentStep: CurrentCount,
                        iTotalSteps: TotalCount,
                        fDisableCancel: false,
                        pfCanceled: out canceled);

                    Interlocked.Exchange(ref WaitDialogUpdateGate, 0);
                }
            }
        }

        private async System.Threading.Tasks.Task RestorePackagesOrCheckForMissingPackages(vsBuildScope scope)
        {
            _msBuildOutputVerbosity = GetMSBuildOutputVerbositySetting(_dte);
            var waitDialogFactory = ServiceLocator.GetGlobalService<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();
            waitDialogFactory.CreateInstance(out _waitDialog);            
            var token = CancellationTokenSource.Token;

            try
            {
                if (IsConsentGranted())
                {
                    if (scope == vsBuildScope.vsBuildScopeSolution || scope == vsBuildScope.vsBuildScopeBatch || scope == vsBuildScope.vsBuildScopeProject)
                    {
                        TotalCount = (await PackageRestoreManager.GetMissingPackagesInSolution(token)).ToList().Count;
                        if (TotalCount > 0)
                        {
                            if (_outputOptOutMessage)
                            {
                                _waitDialog.StartWaitDialog(
                                        Resources.DialogTitle,
                                        Resources.RestoringPackages,
                                        String.Empty,
                                        varStatusBmpAnim: null,
                                        szStatusBarText: null,
                                        iDelayToShowDialog: 0,
                                        fIsCancelable: true,
                                        fShowMarqueeProgress: true);
                                WriteLine(VerbosityLevel.Quiet, Resources.PackageRestoreOptOutMessage);
                                _outputOptOutMessage = false;
                            }

                            System.Threading.Tasks.Task waitDialogCanceledCheckTask = System.Threading.Tasks.Task.Run(() => 
                                {
                                    // Just create an extra task that can keep checking if the wait dialog was cancelled
                                    // If so, cancel the CancellationTokenSource
                                    bool canceled = false;
                                    try
                                    {
                                        while (!canceled && CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested && _waitDialog != null)
                                        {
                                            _waitDialog.HasCanceled(out canceled);
                                            // Wait on the cancellation handle for 100ms to avoid checking on the wait dialog too frequently
                                            CancellationTokenSource.Token.WaitHandle.WaitOne(100);
                                        }

                                        CancellationTokenSource.Cancel();
                                    }
                                    catch (Exception)
                                    {
                                        // Catch all and don't throw
                                        // There is a slight possibility that the _waitDialog was set to null by another thread right after the check for null
                                        // So, it could be null or disposed. Just ignore all errors
                                    }
                                });

                            System.Threading.Tasks.Task whenAllTaskForRestorePackageTasks =
                                System.Threading.Tasks.Task.WhenAll(SolutionManager.GetNuGetProjects().Select(nuGetProject => RestorePackagesInProject(nuGetProject, token)));

                            await System.Threading.Tasks.Task.WhenAny(whenAllTaskForRestorePackageTasks, waitDialogCanceledCheckTask);
                            // Once all the tasks are completed, just cancel the CancellationTokenSource
                            // This will prevent the wait dialog from getting updated
                            CancellationTokenSource.Cancel();                            
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    _waitDialog.StartWaitDialog(
                                    Resources.DialogTitle,
                                    Resources.RestoringPackages,
                                    String.Empty,
                                    varStatusBmpAnim: null,
                                    szStatusBarText: null,
                                    iDelayToShowDialog: 0,
                                    fIsCancelable: true,
                                    fShowMarqueeProgress: true);
                    CheckForMissingPackages((await PackageRestoreManager.GetMissingPackagesInSolution(token)).ToList());
                }
            }
            finally
            {
                int canceled;
                _waitDialog.EndWaitDialog(out canceled);
                _waitDialog = null;
            }

            await PackageRestoreManager.RaisePackagesMissingEventForSolution(CancellationToken.None);
        }

        /// <summary>
        /// Checks if there are missing packages that should be restored. If so, a warning will 
        /// be added to the error list.
        /// </summary>
        private void CheckForMissingPackages(List<PackageReference> missingPackages)
        {
            if (missingPackages.Count > 0)
            {
                var errorText = String.Format(CultureInfo.CurrentCulture,
                    Resources.PackageNotRestoredBecauseOfNoConsent,
                    String.Join(", ", missingPackages.Select(p => p.ToString())));
                ShowError(_errorListProvider, TaskErrorCategory.Error, TaskPriority.High, errorText, hierarchyItem: null);
            }
        }

        private async System.Threading.Tasks.Task RestorePackagesInProject(NuGetProject nuGetProject, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            var projectName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            bool hasMissingPackages = false;
            try
            {
                hasMissingPackages = await PackageRestoreManager.RestoreMissingPackagesAsync(nuGetProject, token);
                WriteLine(hasMissingPackages, error: false);
            }
            catch (Exception ex)
            {
                var exceptionMessage = _msBuildOutputVerbosity >= (int)VerbosityLevel.Detailed ?
                    ex.ToString() :
                    ex.Message;
                var message = String.Format(
                    CultureInfo.CurrentCulture,
                    Resources.PackageRestoreFailedForProject, projectName,
                    exceptionMessage);
                WriteLine(VerbosityLevel.Quiet, message);
                ActivityLog.LogError(LogEntrySource, message);
                ShowError(_errorListProvider, TaskErrorCategory.Error,
                    TaskPriority.High, message, hierarchyItem: null);
                WriteLine(hasMissingPackages, error: true);
            }
            finally
            {
                WriteLine(VerbosityLevel.Normal, Resources.PackageRestoreFinishedForProject, projectName);
            }
        }

        private bool HasCanceled()
        {
            if (_waitDialog != null)
            {
                bool canceled;
                _waitDialog.HasCanceled(out canceled);

                return canceled;
            }
            else
            {
                return false;
            }
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

        private void WriteLine(bool hasMissingPackages, bool error)
        {
            if (hasMissingPackages)
            {
                WriteLine(VerbosityLevel.Normal, Resources.NothingToRestore);
            }

            if (error)
            {
                WriteLine(VerbosityLevel.Minimal, Resources.PackageRestoreFinishedWithError);
            }
            else
            {
                WriteLine(VerbosityLevel.Normal, Resources.PackageRestoreFinished);
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
            else
            {
                return 0;
            }
        }

        private static void ShowError(ErrorListProvider errorListProvider, TaskErrorCategory errorCategory, TaskPriority priority, string errorText, IVsHierarchy hierarchyItem)
        {
            ErrorTask retargetErrorTask = new ErrorTask();
            retargetErrorTask.Text = errorText;
            retargetErrorTask.ErrorCategory = errorCategory;
            retargetErrorTask.Category = TaskCategory.BuildCompile;
            retargetErrorTask.Priority = priority;
            retargetErrorTask.HierarchyItem = hierarchyItem;
            errorListProvider.Tasks.Add(retargetErrorTask);
            errorListProvider.BringToFront();
            errorListProvider.ForceShowErrors();
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
