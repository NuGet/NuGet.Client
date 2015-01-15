using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CommonUI.Types
{
    /// <summary>
    /// Interaction logic for PackageRestoreBar.xaml
    /// </summary>
    public partial class PackageRestoreBar : UserControl
    {
        private readonly IPackageRestoreManager _packageRestoreManager;

        public PackageRestoreBar(IPackageRestoreManager packageRestoreManager)
        {
            InitializeComponent();
            _packageRestoreManager = packageRestoreManager;
            _packageRestoreManager.PackagesMissingStatusChanged += OnPackagesMissingStatusChanged;

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
            _packageRestoreManager.PackagesMissingStatusChanged -= OnPackagesMissingStatusChanged;
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // when the control is first loaded, check for missing packages
            _packageRestoreManager.CheckForMissingPackages();
        }

        private void OnPackagesMissingStatusChanged(object sender, PackagesMissingStatusEventArgs e)
        {
            UpdateRestoreBar(e.PackagesMissing);
        }

        private void UpdateRestoreBar(bool packagesMissing)
        {
            RestoreBar.Visibility = packagesMissing ? Visibility.Visible : Visibility.Collapsed;

            if (packagesMissing)
            {
                ResetUI();
            }
        }

        private void OnRestoreLinkClick(object sender, RoutedEventArgs e)
        {
            ShowProgressUI();
            RestorePackages();
        }

        private void RestorePackages()
        {
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _packageRestoreManager.RestoreMissingPackages().ContinueWith(
                task =>
                {
                    if (task.IsFaulted)
                    {
                        Exception baseException = task.Exception.GetBaseException();
                        ShowErrorUI(baseException.Message);
                        ExceptionHelper.WriteToActivityLog(baseException);
                    }

                    NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageRestoreCompleted);
                },
                uiScheduler);
        }

        private void ResetUI()
        {
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            //StatusMessage.Text = Resources.AskForRestoreMessage;
        }

        private void ShowProgressUI()
        {
            RestoreButton.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Visible;
            //StatusMessage.Text = Resources.PackageRestoreProgressMessage;
        }

        private void ShowErrorUI(string error)
        {
            // re-enable the Restore button to allow users to try again
            RestoreButton.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            //StatusMessage.Text = Resources.PackageRestoreErrorTryAgain + " " + error;
        }
    }
}
