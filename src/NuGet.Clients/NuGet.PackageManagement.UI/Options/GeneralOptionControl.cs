// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.Options
{
    public partial class GeneralOptionControl : UserControl
    {
        private readonly ISettings _settings;
        private bool _initialized;
        private readonly IServiceProvider _serviceprovider;
        private readonly INuGetUILogger _outputConsoleLogger;
        private readonly LocalsCommandRunner _localsCommandRunner;

        public GeneralOptionControl(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            InitializeComponent();
            _settings = ServiceLocator.GetInstance<Configuration.ISettings>();
            _serviceprovider = serviceProvider;
            _outputConsoleLogger = ServiceLocator.GetInstance<INuGetUILogger>();
            _localsCommandRunner = new LocalsCommandRunner();
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
#if !VS14
                    // package management format selection
                    var packageManagement = new PackageManagementFormat(_settings);
                    defaultPackageManagementFormatItems.SelectedIndex = packageManagement.SelectedPackageManagementFormat;
                    showPackageManagementChooser.Checked = packageManagement.Enabled;
#endif
                }
                catch (InvalidOperationException)
                {
                    MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigInvalidOperation, Resources.ErrorDialogBoxTitle);
               } 
                catch (UnauthorizedAccessException)
                {
                    MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigUnauthorizedAccess, Resources.ErrorDialogBoxTitle);
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
#if !VS14
                // package management format selection
                var packageManagement = new PackageManagementFormat(_settings);
                packageManagement.SelectedPackageManagementFormat = defaultPackageManagementFormatItems.SelectedIndex;
                packageManagement.Enabled = showPackageManagementChooser.Checked;
                packageManagement.ApplyChanges();
#endif
            }
            catch (InvalidOperationException)
            {
                MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigInvalidOperation, Resources.ErrorDialogBoxTitle);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigUnauthorizedAccess, Resources.ErrorDialogBoxTitle);
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

        private void packageRestoreConsentCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            packageRestoreAutomaticCheckBox.Enabled = packageRestoreConsentCheckBox.Checked;
            if (!packageRestoreConsentCheckBox.Checked)
            {
                packageRestoreAutomaticCheckBox.Checked = false;
            }
        }

        private void localsCommandButton_OnClick(object sender, EventArgs e)
        {
            updateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandWorking), visibility: true);
            var arguments = new List<string> { "all" };
            var settings = ServiceLocator.GetInstance<ISettings>();
            var logError = new LocalsArgs.Log(LogError);
            var logInformation = new LocalsArgs.Log(LogInformation);
            var localsArgs = new LocalsArgs(arguments, settings, logInformation, logError, clear: true, list: false);
            _outputConsoleLogger.Start();
            try
            {
                _localsCommandRunner.ExecuteCommand(localsArgs);
                updateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandSuccess, DateTime.Now.ToString(Resources.Culture)), visibility: true);
            }
            catch (Exception ex)
            {
                updateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message), visibility: true);
                LogError(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message));
                ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
            }
            finally
            {
                _outputConsoleLogger.End();
            }
        }

        private void updateLocalsCommandStatusText(string statusText, bool visibility)
        {
            localsCommandStatusText.Visible = visibility;
            localsCommandStatusText.Text = statusText;
            localsCommandStatusText.Refresh();
        }

        private void LogError(string message)
        {
            _outputConsoleLogger.Log(MessageLevel.Error, message);
        }

        private void LogInformation(string message)
        {
            _outputConsoleLogger.Log(MessageLevel.Info, message);
        }

        private void localsCommandStatusText_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start(e.LinkText);
        }

        private void localsCommandStatusText_ContentChanged(object sender, ContentsResizedEventArgs e)
        {
            localsCommandStatusText.Height = e.NewRectangle.Height + localsCommandStatusText.Margin.Top + localsCommandStatusText.Margin.Bottom;
            localsCommandStatusText.Width = e.NewRectangle.Width + localsCommandStatusText.Margin.Left + localsCommandStatusText.Margin.Right;
        }

    }
}