// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using VsBrushes = Microsoft.VisualStudio.Shell.VsBrushes;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageRestoreBar.xaml
    /// </summary>
    public partial class PackageRestoreBar : UserControl, INuGetProjectContext
    {
        private IPackageRestoreManager PackageRestoreManager { get; }
        private ISolutionManager SolutionManager { get; }
        private Dispatcher UIDispatcher { get; }
        private Exception RestoreException { get; set; }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ProjectManagement.ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public PackageRestoreBar(ISolutionManager solutionManager, IPackageRestoreManager packageRestoreManager)
        {
            InitializeComponent();
            UIDispatcher = Dispatcher.CurrentDispatcher;
            SolutionManager = solutionManager;
            PackageRestoreManager = packageRestoreManager;

            if (PackageRestoreManager != null)
            {
                PackageRestoreManager.PackagesMissingStatusChanged += OnPackagesMissingStatusChanged;
            }

            // Set DynamicResource binding in code
            // The reason we can't set it in XAML is that the VsBrushes class comes from either
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly,
            // depending on whether NuGet runs inside VS10 or VS11.
            StatusMessage.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.InfoTextKey);
            RestoreBar.SetResourceReference(Border.BackgroundProperty, VsBrushes.InfoBackgroundKey);
            RestoreBar.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey);
        }

        public void CleanUp()
        {
            if (PackageRestoreManager != null)
            {
                PackageRestoreManager.PackagesMissingStatusChanged -= OnPackagesMissingStatusChanged;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (PackageRestoreManager != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    try
                    {
                        var solutionDirectory = SolutionManager.SolutionDirectory;

                        // when the control is first loaded, check for missing packages
                        await PackageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // By default, restore bar is invisible. So, in case of failure of RaisePackagesMissingEventForSolutionAsync, assume it is needed
                        UpdateRestoreBar(packagesMissing: true);
                        var unwrappedException = ExceptionUtility.Unwrap(ex);
                        ShowErrorUI(unwrappedException.Message);
                    }
                });
            }
        }

        private void OnPackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            UpdateRestoreBar(e.PackagesMissing);
        }

        private void UpdateRestoreBar(bool packagesMissing)
        {
            if (!UIDispatcher.CheckAccess())
            {
                UIDispatcher.Invoke(
                    (Action<bool>)UpdateRestoreBar,
                    packagesMissing);
                return;
            }

            if (packagesMissing)
            {
                ResetUI();
            }
            else
            {
                RestoreBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OnRestoreLinkClick(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => UIRestorePackagesAsync(CancellationToken.None));
        }

        public async Task<bool> UIRestorePackagesAsync(CancellationToken token)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ShowProgressUI();

            try
            {
                PackageRestoreManager.PackageRestoreFailedEvent += PackageRestoreFailedEvent;
                var solutionDirectory = SolutionManager.SolutionDirectory;
                await PackageRestoreManager.RestoreMissingPackagesInSolutionAsync(solutionDirectory,
                    this,
                    token);

                if (RestoreException == null)
                {
                    // when the control is first loaded, check for missing packages
                    await PackageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, token);
                }
                else
                {
                    ShowErrorUI(RestoreException.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowErrorUI(ex.Message);
                return false;
            }
            finally
            {
                PackageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreFailedEvent;
                RestoreException = null;
            }

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageRestoreCompleted);
            return true;
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            ShowMessage(String.Format(CultureInfo.CurrentCulture, message, args));
        }

        public void ReportError(string message)
        {
            ShowMessage(message);
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        private void PackageRestoreFailedEvent(object sender, PackageRestoreFailedEventArgs e)
        {
            // We just store any one of the package restore failures and show it on the yellow bar
            if(RestoreException == null)
            {
                RestoreException = e.Exception;
            }
        }

        private void ResetUI()
        {
            RestoreBar.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusMessage.Text = UI.Resources.AskForRestoreMessage;
        }

        private void ShowProgressUI()
        {
            RestoreBar.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Visible;
            StatusMessage.Text = UI.Resources.PackageRestoreProgressMessage;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Controls.TextBlock.set_Text(System.String)")]
        private void ShowErrorUI(string error)
        {
            // re-enable the Restore button to allow users to try again
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusMessage.Text = UI.Resources.PackageRestoreErrorTryAgain + " " + error;
        }

        private void ShowMessage(string message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                StatusMessage.Text = message;
            });
        }
    }
}
