// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using NuGet.Configuration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.Services
{
    [Guid("6C09BBE2-4537-48B4-87D8-01BF5EB75901")]
    public sealed class ExternalSettingsProviderService : IExternalSettingsProvider
    {
        private const string MonikerAllowRestoreDownload = "packageRestore.allowRestoreDownload";
        private const string MonikerPackageRestoreAutomatic = "packageRestore.packageRestoreAutomatic";
        private const string MonikerSkipBindingRedirects = "bindingRedirects.skipBindingRedirects";
        private const string MonikerDefaultPackageManagementFormat = "packageManagement.defaultPackageManagementFormat";
        //package-reference
        //packages-config
        private const string MonikerShowPackageManagementChooser = "packageManagement.showPackageManagementChooser";

        private readonly ISettings _settings;
        //private readonly INuGetUILogger _outputConsoleLogger;
        //private readonly LocalsCommandRunner _localsCommandRunner;

        private PackageRestoreConsent _packageRestoreConsent; //TODO: reset this when nuget.configs change
        private BindingRedirectBehavior _bindingRedirectBehavior; //TODO: reset this when nuget.configs change
        private PackageManagementFormat _packageManagementFormat; //TODO: reset this when nuget.configs change

        public ExternalSettingsProviderService()
        {
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            _settings = componentModel.GetService<ISettings>();
            //_outputConsoleLogger = componentModel.GetService<INuGetUILogger>();
            //_localsCommandRunner = new LocalsCommandRunner();
            Debug.Assert(_settings != null);
        }
        private BindingRedirectBehavior BindingRedirectBehavior
        {
            get
            {
                if (_bindingRedirectBehavior is null)
                {
                    _bindingRedirectBehavior = new BindingRedirectBehavior(_settings);
                }

                return _bindingRedirectBehavior;
            }
        }

        private PackageRestoreConsent PackageRestoreConsent
        {
            get
            {
                if (_packageRestoreConsent is null)
                {
                    _packageRestoreConsent = new PackageManagement.PackageRestoreConsent(_settings);
                }

                return _packageRestoreConsent;
            }
        }

        private PackageManagementFormat PackageManagementFormat
        {
            get
            {
                if (_packageManagementFormat is null)
                {
                    _packageManagementFormat = new PackageManagementFormat(_settings);
                }

                return _packageManagementFormat;
            }
        }

        public event EventHandler<ExternalSettingsChangedEventArgs> SettingValuesChanged { add { } remove { } }
        public event EventHandler<EnumSettingChoicesChangedEventArgs> EnumSettingChoicesChanged { add { } remove { } }
        public event EventHandler<DynamicMessageTextChangedEventArgs> DynamicMessageTextChanged { add { } remove { } }
        public event EventHandler ErrorConditionResolved { add { } remove { } }

        public void Dispose()
        {
        }

        public Task<ExternalSettingOperationResult<IReadOnlyList<EnumChoice>>> GetEnumChoicesAsync(string enumSettingMoniker, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetMessageTextAsync(string messageId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalSettingOperationResult<T>> GetValueAsync<T>(string moniker, CancellationToken cancellationToken) where T : notnull
        {
            switch (moniker)
            {
                case MonikerAllowRestoreDownload: return ConvertValueOrThrow<T>(PackageRestoreConsent.IsGrantedInSettings);
                case MonikerPackageRestoreAutomatic: return ConvertValueOrThrow<T>(PackageRestoreConsent.IsAutomatic);
                case MonikerSkipBindingRedirects: return ConvertValueOrThrow<T>(BindingRedirectBehavior.IsSkipped);
                case MonikerDefaultPackageManagementFormat: return ConvertValueOrThrow<T>(PackageManagementFormat.PackageFormatSelectorLabel);
                default: break;
                    //return Task.FromResult(new ExternalSettingOperationResult.Failure("Error reading setting!", ExternalSettingsErrorScope.SingleSettingOnly, isTransient: true));
            }

            throw new ApplicationException("Unknown setting!");

            // TODO ?
            //    // Thrown during creating or saving NuGet.Config.
            //    catch (NuGetConfigurationException ex)
            //    {
            //    MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
            //}
            //    // Thrown if no nuget.config found.
            //    catch (InvalidOperationException ex)
            //    {
            //    MessageHelper.ShowErrorMessage(ex.Message, Resources.ErrorDialogBoxTitle);
            //}
            //    catch (UnauthorizedAccessException)
            //    {
            //    MessageHelper.ShowErrorMessage(Resources.ShowError_ConfigUnauthorizedAccess, Resources.ErrorDialogBoxTitle);
            //}
            //    // Unknown exception.
            //    catch (Exception ex)
            //    {
            //    MessageHelper.ShowErrorMessage(Resources.ShowError_SettingActivatedFailed, Resources.ErrorDialogBoxTitle);
            //    ActivityLog.LogError(NuGetUI.LogEntrySource, ex.ToString());
            //}
        }

        public Task OpenBackingStoreAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalSettingOperationResult> SetValueAsync<T>(string moniker, T value, CancellationToken cancellationToken) where T : notnull
        {
            switch (moniker)
            {
                case MonikerAllowRestoreDownload:
                    {
                        return SetValueOrThrow(value, () => PackageRestoreConsent.IsGrantedInSettings);
                    }
                case MonikerPackageRestoreAutomatic:
                    {
                        if (value is bool boolValue)
                        {
                            PackageRestoreConsent.IsAutomatic = boolValue;
                            return Task.FromResult((ExternalSettingOperationResult)ExternalSettingOperationResult.Success.Instance);
                        }
                        break;
                    }
                case MonikerSkipBindingRedirects: return SetValueOrThrow<T>(BindingRedirectBehavior.IsSkipped);
                case MonikerDefaultPackageManagementFormat: return SetValueOrThrow<T>(PackageManagementFormat.PackageFormatSelectorLabel);
                default: break;
                    //return Task.FromResult(new ExternalSettingOperationResult.Failure("Error reading setting!", ExternalSettingsErrorScope.SingleSettingOnly, isTransient: true));
            }

            throw new ApplicationException("Unknown setting!");
        }


        private static Task<ExternalSettingOperationResult<T>> ConvertValueOrThrow<T>(object input) where T : notnull
        {
            if (input is T value)
            {
                return Task.FromResult(ExternalSettingOperationResult.SuccessResult(value));
            }

            throw new ApplicationException("Error reading setting!");
        }

        private static ExternalSettingOperationResult SetValueOrThrow<T, TValue>(T originValue, ref T2 destination) where T : notnull
        {
            if (originValue is T2 destinationValue)
            {
                destination = destinationValue;
                return (ExternalSettingOperationResult)ExternalSettingOperationResult.Success.Instance;
            }

            throw new ApplicationException("Error saving setting!");
        }
    }
}
