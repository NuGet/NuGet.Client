using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using System.Threading;
using System.Windows.Threading;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageRestoreBar.xaml
    /// </summary>
    public partial class PackageRestoreBar : UserControl
    {
        private readonly IPackageRestoreManager _packageRestoreManager;
        private Dispatcher _uiDispatcher;

        public PackageRestoreBar(IPackageRestoreManager packageRestoreManager)
        {
            InitializeComponent();
            _uiDispatcher = Dispatcher.CurrentDispatcher;
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
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_packageRestoreManager != null)
            {
                // when the control is first loaded, check for missing packages
                await _packageRestoreManager.RaisePackagesMissingEventForSolution(CancellationToken.None);
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

        private async void OnRestoreLinkClick(object sender, RoutedEventArgs e)
        {
            ShowProgressUI();
            await RestorePackages();
        }

        private async System.Threading.Tasks.Task RestorePackages()
        {
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            try
            {
                await _packageRestoreManager.RestoreMissingPackagesInSolutionAsync(CancellationToken.None);
                // Check for missing packages again
                await _packageRestoreManager.RaisePackagesMissingEventForSolution(CancellationToken.None);
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