// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;
using VsBrushes = Microsoft.VisualStudio.Shell.VsBrushes;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageRestoreBar.xaml
    /// </summary>
    public partial class PackageRestoreBar : UserControl, INuGetProjectContext
    {
        private readonly IPackageRestoreManager _packageRestoreManager;
        // This class does not own this instance, so do not dispose of it in this class.
        private readonly INuGetSolutionManagerService _solutionManager;
        private readonly Dispatcher _uiDispatcher;
        private Exception _restoreException;
        private Storyboard _showRestoreBar;
        private Storyboard _hideRestoreBar;

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ProjectManagement.ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public NuGetActionType ActionType { get; set; }

        public Guid OperationId { get; set; }

        public Visibility InnerVisibility
        {
            get { return (Visibility)GetValue(InnerVisibilityProperty); }
            set { SetValue(InnerVisibilityProperty, value); }
        }

        public static readonly DependencyProperty InnerVisibilityProperty =
            DependencyProperty.Register(nameof(InnerVisibility), typeof(Visibility), typeof(PackageRestoreBar), new PropertyMetadata(Visibility.Collapsed));

        public PackageRestoreBar(INuGetSolutionManagerService solutionManager, IPackageRestoreManager packageRestoreManager)
        {
            DataContext = this;
            InitializeComponent();
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _solutionManager = solutionManager;
            _packageRestoreManager = packageRestoreManager;

            if (_packageRestoreManager != null)
            {
                _packageRestoreManager.PackagesMissingStatusChanged += OnPackagesMissingStatusChanged;
            }

            // Set DynamicResource binding in code
            // The reason we can't set it in XAML is that the VsBrushes class comes from either
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly,
            // depending on whether NuGet runs inside VS10 or VS11.
            StatusMessage.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.InfoTextKey);
            RestoreBar.SetResourceReference(Border.BackgroundProperty, VsBrushes.InfoBackgroundKey);
            RestoreBar.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey);

            // Find storyboards that will be used to smoothly show and hide the restore bar.
            _showRestoreBar = FindResource("ShowSmoothly") as Storyboard;
            _hideRestoreBar = FindResource("HideSmoothly") as Storyboard;
        }

        public void CleanUp()
        {
            if (_packageRestoreManager != null)
            {
                _packageRestoreManager.PackagesMissingStatusChanged -= OnPackagesMissingStatusChanged;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_packageRestoreManager != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    try
                    {
                        string solutionDirectory = await _solutionManager.GetSolutionDirectoryAsync(CancellationToken.None);

                        // when the control is first loaded, check for missing packages
                        await _packageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                        await _packageRestoreManager.RaiseAssetsFileMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        // By default, restore bar is invisible. So, in case of failure of RaisePackagesMissingEventForSolutionAsync, assume it is needed
                        UpdateRestoreBar(packagesMissing: true);
                        var unwrappedException = ExceptionUtility.Unwrap(ex);
                        ShowErrorUI(unwrappedException.Message);
                    }
                }).PostOnFailure(nameof(PackageRestoreBar));
            }
        }

        private void OnPackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            // make sure update happens on the UI thread.
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UpdateRestoreBar(e.PackagesMissing);
            });
        }

        private void UpdateRestoreBar(bool packagesMissing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (packagesMissing)
            {
                ResetUI();
            }
            else
            {
                // In order to hide the restore bar:
                // * stop the reveal animation, in case it was still going.
                // * begin the hide animation.
                _showRestoreBar.Stop();
                _hideRestoreBar.Begin();
            }
        }

        private void OnRestoreLinkClick(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => UIRestorePackagesAsync(CancellationToken.None)).PostOnFailure(nameof(PackageRestoreBar));
        }

        public async Task<bool> UIRestorePackagesAsync(CancellationToken token)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ShowProgressUI();
            OperationId = Guid.NewGuid();

            try
            {
                _packageRestoreManager.PackageRestoreFailedEvent += PackageRestoreFailedEvent;
                string solutionDirectory = await _solutionManager.GetSolutionDirectoryAsync(token);

                if (await _packageRestoreManager.GetMissingAssetsFileStatusAsync())
                {
                    await _packageRestoreManager.RestoreMissingAssetsFileInSolutionAsync(solutionDirectory,
                        this,
                        new LoggerAdapter(this),
                        token);
                }
                else
                {
                    await _packageRestoreManager.RestoreMissingPackagesInSolutionAsync(solutionDirectory,
                        this,
                        new LoggerAdapter(this),
                        token);
                }

                if (_restoreException == null)
                {
                    // when the control is first loaded, check for missing packages and missing assets files
                    await _packageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, token);
                    await _packageRestoreManager.RaiseAssetsFileMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                }
                else
                {
                    ShowErrorUI(_restoreException.Message);
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
                _packageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreFailedEvent;
                _restoreException = null;
            }

            return true;
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            if (args.Length > 0)
            {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }

            ShowMessage(message);
        }

        public void Log(ILogMessage message)
        {
            ShowMessage(message.FormatWithCode());
        }

        public void ReportError(string message)
        {
            ShowMessage(message);
        }

        public void ReportError(ILogMessage message)
        {
            ShowMessage(message.FormatWithCode());
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        private void PackageRestoreFailedEvent(object sender, PackageRestoreFailedEventArgs e)
        {
            // We just store any one of the package restore failures and show it on the yellow bar
            if (_restoreException == null)
            {
                _restoreException = e.Exception;
            }
        }

        private void RevealRestoreBar()
        {
            // If the restoreBar isn't visible, begin the animation to reveal it.
            if (RestoreBar.Visibility != Visibility.Visible)
            {
                _showRestoreBar.Begin();
            }
        }

        private void ResetUI()
        {
            RevealRestoreBar();
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusMessage.Text = UI.Resources.AskForRestoreMessage;
        }

        private void ShowProgressUI()
        {
            RevealRestoreBar();
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
            }).PostOnFailure(nameof(PackageRestoreBar));
        }
    }
}
