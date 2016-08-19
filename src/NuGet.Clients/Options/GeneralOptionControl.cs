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
using NuGetConsole;
using static NuGet.Commands.LocalsCommandRunner;

namespace NuGet.Options
{
    public partial class GeneralOptionControl : UserControl
    {
        private readonly Configuration.ISettings _settings;
        private bool _initialized;
        private IServiceProvider _serviceprovider;
        private OutputConsoleLogger _outputConsoleLogger;

        public GeneralOptionControl(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _settings = ServiceLocator.GetInstance<Configuration.ISettings>();
            _serviceprovider = serviceProvider;
            _outputConsoleLogger = new OutputConsoleLogger(_serviceprovider);
            Debug.Assert(_settings != null);
        }

        internal void OnActivated()
        {
            if (!_initialized)
            {
                try
                {
                    // not using the nuget.core version of PackageRestoreConsent
                    var packageRestoreConsent = new PackageManagement.VisualStudio.PackageRestoreConsent(_settings);

                    packageRestoreConsentCheckBox.Checked = packageRestoreConsent.IsGrantedInSettings;
                    packageRestoreAutomaticCheckBox.Checked = packageRestoreConsent.IsAutomatic;
                    packageRestoreAutomaticCheckBox.Enabled = packageRestoreConsentCheckBox.Checked;

                    var bindingRedirects = new BindingRedirectBehavior(_settings);
                    skipBindingRedirects.Checked = bindingRedirects.IsSkipped;
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
                var packageRestoreConsent = new PackageManagement.VisualStudio.PackageRestoreConsent(_settings);
                packageRestoreConsent.IsGrantedInSettings = packageRestoreConsentCheckBox.Checked;
                packageRestoreConsent.IsAutomatic = packageRestoreAutomaticCheckBox.Checked;

                var bindingRedirects = new BindingRedirectBehavior(_settings);
                bindingRedirects.IsSkipped = skipBindingRedirects.Checked;
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
            updateLocalsCommandStatusText(String.Format(Resources.ShowMessage_LocalsCommandWorking), visibility: true);
            var arguments = new List<string> { "all" };
            var settings = ServiceLocator.GetInstance<ISettings>();            
            Log logError = new Log(LogError);
            Log logInformation = new Log(LogInformation);
            _outputConsoleLogger.Start();
            var localsCommandRunner = new LocalsCommandRunner(arguments, settings, logInformation, logError, clear: true, list: false);
            try
            {
                localsCommandRunner.ExecuteCommand();
            }
            catch (Exception ex)
            {
                updateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message), visibility: true);
                LogError(string.Format(Resources.ShowMessage_LocalsCommandFailure, DateTime.Now.ToString(Resources.Culture), ex.Message));
                ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
            }
            if (localsCommandRunner.Result == LocalsCommandResult.ClearSuccess)
            {
                updateLocalsCommandStatusText(string.Format(Resources.ShowMessage_LocalsCommandSuccess, DateTime.Now.ToString(Resources.Culture)), visibility: true);
            }
            _outputConsoleLogger.End();
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
    }
}