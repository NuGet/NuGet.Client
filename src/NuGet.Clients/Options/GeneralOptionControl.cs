// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Windows.Forms;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.Options
{
    public partial class GeneralOptionControl : UserControl
    {
        private readonly Configuration.ISettings _settings;
        private bool _initialized;

        public GeneralOptionControl()
        {
            InitializeComponent();

            _settings = ServiceLocator.GetInstance<Configuration.ISettings>();
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
                catch(InvalidOperationException)
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
    }
}
