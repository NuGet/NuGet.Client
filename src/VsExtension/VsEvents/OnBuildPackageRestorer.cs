using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using System;
using System.Globalization;

namespace NuGetVSExtension
{
    public sealed class OnBuildPackageRestorer
    {
        private const string LogEntrySource = "NuGet PackageRestorer";

        private DTE _dte;

        // The value of the "MSBuild project build output verbosity" setting 
        // of VS. From 0 (quiet) to 4 (Diagnostic).
        private int _msBuildOutputVerbosity;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected.
        private BuildEvents _buildEvents;

        private SolutionEvents _solutionEvents;

        private ErrorListProvider _errorListProvider;

        private IVsThreadedWaitDialog2 _waitDialog;

        private IPackageRestoreManager PackageRestoreManager { get; set; }

        enum VerbosityLevel
        {
            Quiet = 0,
            Minimal = 1,
            Normal = 2,
            Detailed = 3,
            Diagnostic = 4
        };

        public OnBuildPackageRestorer(IPackageRestoreManager packageRestoreManager, IServiceProvider serviceProvider)
        {
            PackageRestoreManager = packageRestoreManager;
            _dte = ServiceLocator.GetInstance<DTE>();
            _errorListProvider = new ErrorListProvider(serviceProvider);
            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;   
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
            try
            {
                _errorListProvider.Tasks.Clear();

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
        }

        private async System.Threading.Tasks.Task RestorePackagesOrCheckForMissingPackages(vsBuildScope scope)
        {
            _msBuildOutputVerbosity = GetMSBuildOutputVerbositySetting(_dte);
            var waitDialogFactory = ServiceLocator.GetGlobalService<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();
            waitDialogFactory.CreateInstance(out _waitDialog);

            try
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
                if (IsConsentGranted())
                {
                    bool hasMissingPackages = false;
                    try
                    {
                        hasMissingPackages = await PackageRestoreManager.RestoreMissingPackagesInSolution();
                        WriteLine(hasMissingPackages, error: false);
                    }
                    catch (Exception)
                    {
                        WriteLine(hasMissingPackages, error: true);
                    }
                    PackageRestoreManager.RaisePackagesMissingEventForSolution();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            finally
            {
                int canceled;
                _waitDialog.EndWaitDialog(out canceled);
                _waitDialog = null;
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
            return true;
            //var settings = ServiceLocator.GetInstance<ISettings>();
            //var packageRestoreConsent = new PackageRestoreConsent(settings);
            //return packageRestoreConsent.IsGranted;
        }

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        /// <returns>True if automatic package restore on build is enabled.</returns>
        private static bool IsAutomatic()
        {
            return true;
            //var settings = ServiceLocator.GetInstance<ISettings>();
            //var packageRestoreConsent = new PackageRestoreConsent(settings);
            //return packageRestoreConsent.IsAutomatic;
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

        public void Dispose()
        {
            _errorListProvider.Dispose();
            _buildEvents.OnBuildBegin -= BuildEvents_OnBuildBegin;
            _solutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
        }
    }
}
