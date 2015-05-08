using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageRestoreBar.xaml
    /// </summary>
    public partial class PackageRestoreBar : UserControl
    {
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly ISolutionManager _solutionManager;
        private Dispatcher _uiDispatcher;

        public PackageRestoreBar(ISolutionManager solutionManager, IPackageRestoreManager packageRestoreManager)
        {
            InitializeComponent();
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _solutionManager = solutionManager;
            _packageRestoreManager = packageRestoreManager;

            if (_packageRestoreManager != null)
            {
                _packageRestoreManager.PackagesMissingStatusChanged += OnPackagesMissingStatusChanged;
            }

            // Set DynamicResource binding in code 
            // The reason we can't set it in XAML is that the VsBrushes class come from either 
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly, 
            // depending on whether NuGet runs inside VS10 or VS11.
            StatusMessage.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.InfoTextKey);
            RestoreBar.SetResourceReference(Border.BackgroundProperty, VsBrushes.InfoBackgroundKey);
            RestoreBar.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey);
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
            // Loaded should only fire once
            Loaded -= UserControl_Loaded;

            if (_packageRestoreManager != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    var solutionDirectory = _solutionManager.SolutionDirectory;
                    // when the control is first loaded, check for missing packages
                    await _packageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                });
            }
        }

        private void OnPackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            UpdateRestoreBar(e.PackagesMissing);
        }

        private void UpdateRestoreBar(bool packagesMissing)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Invoke(
                    new Action<bool>(UpdateRestoreBar),
                    packagesMissing);
                return;
            }

            RestoreBar.Visibility = packagesMissing ? Visibility.Visible : Visibility.Collapsed;
            if (packagesMissing)
            {
                ResetUI();
            }
        }

        private void OnRestoreLinkClick(object sender, RoutedEventArgs e)
        {
            ShowProgressUI();
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await RestorePackagesAsync();
            });
        }

        private async Task RestorePackagesAsync()
        {
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            try
            {
                var solutionDirectory = _solutionManager.SolutionDirectory;
                await _packageRestoreManager.RestoreMissingPackagesInSolutionAsync(solutionDirectory, CancellationToken.None);
                // Check for missing packages again
                await _packageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
            }
            catch (Exception ex)
            {
                ShowErrorUI(ex.Message);
            }

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageRestoreCompleted);
        }

        private void ResetUI()
        {
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusMessage.Text = NuGet.PackageManagement.UI.Resources.AskForRestoreMessage;
        }

        private void ShowProgressUI()
        {
            RestoreButton.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Visible;
            StatusMessage.Text = NuGet.PackageManagement.UI.Resources.PackageRestoreProgressMessage;
        }

        private void ShowErrorUI(string error)
        {
            // re-enable the Restore button to allow users to try again
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusMessage.Text = NuGet.PackageManagement.UI.Resources.PackageRestoreErrorTryAgain + " " + error;
        }
    }
}