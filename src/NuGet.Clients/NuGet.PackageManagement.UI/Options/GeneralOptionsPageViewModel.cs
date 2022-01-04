// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.Options
{
    public class GeneralOptionsPageViewModel : ViewModelBase
    {
        private readonly PackageRestoreConsent _packageRestoreConsent;
        private readonly BindingRedirectBehavior _bindingRedirectBehavior;
        private readonly PackageManagementFormat _packageManagementFormat;
        private readonly LocalsCommandRunner _commandRunner;
        private readonly INuGetUILogger _logger;

        private bool _isRestoreConsentGranted;
        private bool _isRestoreAutomatic;
        private bool _isSkipBindingRedirects;
        private bool _isPackageManagementSelectionShown;
        private int _selectedPackageManagementFormat;


        public GeneralOptionsPageViewModel(
            PackageRestoreConsent packageRestoreConsent,
            PackageManagementFormat packageManagementFormat,
            BindingRedirectBehavior bindingRedirectBehavior,
            LocalsCommandRunner commandRunner,
            INuGetUILogger logger)
        {
            _packageRestoreConsent = packageRestoreConsent ?? throw new ArgumentNullException(nameof(packageRestoreConsent));
            _bindingRedirectBehavior = bindingRedirectBehavior ?? throw new ArgumentNullException(nameof(bindingRedirectBehavior));
            _packageManagementFormat = packageManagementFormat ?? throw new ArgumentNullException(nameof(packageManagementFormat));
            _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsRestoreConsentGranted
        {
            get => _isRestoreConsentGranted;
            set => SetValue(ref _isRestoreConsentGranted, value);
        }

        public bool IsRestoreAutomatic
        {
            get => _isRestoreAutomatic;
            set => SetValue(ref _isRestoreAutomatic, value);
        }

        public bool IsSkipBindingRedirects
        {
            get => _isSkipBindingRedirects;
            set => SetValue(ref _isSkipBindingRedirects, value);
        }

        public bool IsPackageManagementSelectionShown
        {
            get => _isPackageManagementSelectionShown;
            set => SetValue(ref _isPackageManagementSelectionShown, value);
        }

        public int SelectedPackageManagementFormat
        {
            get => _selectedPackageManagementFormat;
            set => SetValue(ref _selectedPackageManagementFormat, value);
        }

        public void Refresh()
        {
            try
            {
                IsRestoreConsentGranted = _packageRestoreConsent.IsGrantedInSettings;
                IsRestoreAutomatic = _packageRestoreConsent.IsAutomatic;
                IsSkipBindingRedirects = _bindingRedirectBehavior.IsSkipped;
                IsPackageManagementSelectionShown = _packageManagementFormat.Enabled;
                SelectedPackageManagementFormat = _packageManagementFormat.SelectedPackageManagementFormat;
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

        public bool ApplyChanges()
        {
            bool isApplySuccessful = false;

            try
            {
                _packageRestoreConsent.IsGrantedInSettings = IsRestoreConsentGranted;
                _packageRestoreConsent.IsAutomatic = IsRestoreAutomatic;

                _bindingRedirectBehavior.IsSkipped = IsSkipBindingRedirects;

                _packageManagementFormat.SelectedPackageManagementFormat = SelectedPackageManagementFormat;
                _packageManagementFormat.Enabled = IsPackageManagementSelectionShown;
                _packageManagementFormat.ApplyChanges();

                isApplySuccessful = true;
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
                MessageHelper.ShowErrorMessage(Resources.ShowError_ApplySettingFailed, Resources.ErrorDialogBoxTitle);
                ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
            }

            return isApplySuccessful;
        }
    }
}
