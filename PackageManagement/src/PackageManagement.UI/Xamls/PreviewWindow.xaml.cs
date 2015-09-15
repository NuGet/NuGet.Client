// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InstallPreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : VsDialogWindow
    {
        private bool _initialized;
        private INuGetUIContext _uiContext;

        public PreviewWindow(INuGetUIContext uiContext)
        {
            _initialized = false;
            _uiContext = uiContext;
            InitializeComponent();
            _doNotShowCheckBox.IsChecked = IsDoNotShowPreviewWindowEnabled();

            if (StandaloneSwitch.IsRunningStandalone)
            {
                Background = SystemColors.WindowBrush;
            }
            _initialized = true;
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void DoNotShowCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SaveDoNotShowPreviewWindowSetting(doNotshow: true);
        }

        private void DoNotShowCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            SaveDoNotShowPreviewWindowSetting(doNotshow: false);
        }

        private void SaveDoNotShowPreviewWindowSetting(bool doNotshow)
        {
            _uiContext.ApplyShowPreviewSetting(!doNotshow);
            RegistrySettingUtility.SetBooleanSetting(
                Constants.DoNotShowPreviewWindowRegistryName,
                doNotshow);
        }

        public static bool IsDoNotShowPreviewWindowEnabled()
        {
            return RegistrySettingUtility.GetBooleanSetting(
                Constants.DoNotShowPreviewWindowRegistryName);
        }
    }
}