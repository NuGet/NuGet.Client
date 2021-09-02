// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.Options
{
    public partial class GeneralOptionControl : UserControl
    {
        private readonly ISettings _settings;
        private bool _initialized;
        private readonly INuGetUILogger _outputConsoleLogger;
        private readonly LocalsCommandRunner _localsCommandRunner;

        public GeneralOptionControl()
        {
            InitializeComponent();
            _settings = ServiceLocator.GetInstance<Configuration.ISettings>();
            _outputConsoleLogger = ServiceLocator.GetInstance<INuGetUILogger>();
            _localsCommandRunner = new LocalsCommandRunner();
            AutoScroll = true;
            Debug.Assert(_settings != null);
        }

        internal void OnActivated()
        {
            if (!_initialized)
            {
                try
                {
                    var packageRestoreConsent = new PackageManagement.PackageRestoreConsent(_settings);

                    packageRestoreConsentCheckBox.Checked = packageRestoreConsent.IsGrantedInSettings;
                    packageRestoreAutomaticCheckBox.Checked = packageRestoreConsent.IsAutomatic;
                    packageRestoreAutomaticCheckBox.Enabled = packageRestoreConsentCheckBox.Checked;

                    var bindingRedirects = new BindingRedirectBehavior(_settings);
                    skipBindingRedirects.Checked = bindingRedirects.IsSkipped;

                    // package management format selection
                    var packageManagement = new PackageManagementFormat(_settings);
                    defaultPackageManagementFormatItems.SelectedIndex = packageManagement.SelectedPackageManagementFormat;
                    showPackageManagementChooser.Checked = packageManagement.Enabled;
                }
                // Thrown during creating or saving NuGet.Config.
                catch (NuGetConfigurationException ex)
                {
                    MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
                }
                // Thrown if no nuget.config found.
                catch (InvalidOperationException ex)
                {
                    MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
                }
                catch (UnauthorizedAccessException)
                {
                    MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigUnauthorizedAccess, Resources.ErrorDialogBoxTitle);
                }
                // Unknown exception.
                catch (Exception ex)
                {
                    MessageHelper.ShowErrorMessage(Resources.ShowError_SettingActivatedFailed, Resources.ErrorDialogBoxTitle);
                    ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
                }
            }

            _initialized = true;
        }

        internal bool OnApply()
        {
            try
            {
                var packageRestoreConsent = new PackageManagement.PackageRestoreConsent(_settings);
                packageRestoreConsent.IsGrantedInSettings = packageRestoreConsentCheckBox.Checked;
                packageRestoreConsent.IsAutomatic = packageRestoreAutomaticCheckBox.Checked;

                var bindingRedirects = new BindingRedirectBehavior(_settings);
                bindingRedirects.IsSkipped = skipBindingRedirects.Checked;

                // package management format selection
                var packageManagement = new PackageManagementFormat(_settings);
                packageManagement.SelectedPackageManagementFormat = defaultPackageManagementFormatItems.SelectedIndex;
                packageManagement.Enabled = showPackageManagementChooser.Checked;
                packageManagement.ApplyChanges();
            }
            // Thrown during creating or saving NuGet.Config.
            catch (NuGetConfigurationException ex)
            {
                MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
                return false;
            }
            // Thrown if no nuget.config found.
            catch (InvalidOperationException ex)
            {
                MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigUnauthorizedAccess, Resources.ErrorDialogBoxTitle);
                return false;
            }
            // Unknown exception.
            catch (Exception ex)
            {
                MessageHelper.ShowErrorMessage(Resources.ShowError_ApplySettingFailed, Resources.ErrorDialogBoxTitle);
                ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
                return false;
            }

            return true;
        }

        internal void OnClosed()
        {
            _initialized = false;
        }

        private void OnClearPackageCacheClick(object sender, EventArgs e)
        {
            //not implement now
        }

        private void OnBrowsePackageCacheClick(object sender, EventArgs e)
        {
            //not impement now
        }

        private void OnPackageRestoreConsentCheckBoxCheckedChanged(object sender, EventArgs e)
        {
            packageRestoreAutomaticCheckBox.Enabled = packageRestoreConsentCheckBox.Checked;
            if (!packageRestoreConsentCheckBox.Checked)
            {
                packageRestoreAutomaticCheckBox.Checked = false;
            }
        }

        private void OnLocalsCommandButtonOnClick(object sender, EventArgs e)
        {
            UpdateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandWorking), visibility: true);
            var arguments = new List<string> { "all" };
            var settings = ServiceLocator.GetInstance<ISettings>();
            var logError = new LocalsArgs.Log(LogError);
            var logInformation = new LocalsArgs.Log(LogInformation);
            var localsArgs = new LocalsArgs(arguments, settings, logInformation, logError, clear: true, list: false);
            _outputConsoleLogger.Start();
            try
            {
                _localsCommandRunner.ExecuteCommand(localsArgs);
                UpdateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandSuccess, DateTime.Now.ToString(Resources.Culture)), visibility: true);
            }
            catch (Exception ex)
            {
                UpdateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message), visibility: true);
                LogError(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message));
                ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
            }
            finally
            {
                _outputConsoleLogger.End();
            }
        }

        private void UpdateLocalsCommandStatusText(string statusText, bool visibility)
        {
            localsCommandStatusText.AccessibleName = statusText;
            localsCommandStatusText.Visible = visibility;
            localsCommandStatusText.Text = statusText;
            localsCommandStatusText.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            localsCommandStatusText.Refresh();
        }

        private void LogError(string message)
        {
            _outputConsoleLogger.Log(new LogMessage(LogLevel.Error, message));
        }

        private void LogInformation(string message)
        {
            _outputConsoleLogger.Log(new LogMessage(LogLevel.Information, message));
        }

        private void OnLocalsCommandStatusTextLinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start(e.LinkText);
        }

        private void OnLocalsCommandStatusTextContentChanged(object sender, ContentsResizedEventArgs e)
        {
            localsCommandStatusText.Height = e.NewRectangle.Height + localsCommandStatusText.Margin.Top + localsCommandStatusText.Margin.Bottom;
        }
    }
}
