// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.Credentials;
using NuGet.ProjectManagement;
using NuGet.Protocol.Plugins;
using NuGet.VisualStudio;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ICredentialServiceProvider))]
    public class DefaultVSCredentialServiceProvider : ICredentialServiceProvider
    {

        private readonly Lazy<INuGetUILogger> _outputConsoleLogger;
        private readonly IAsyncServiceProvider _asyncServiceProvider;

        [ImportingConstructor]
        internal DefaultVSCredentialServiceProvider(Lazy<INuGetUILogger> outputConsoleLogger)
            : this(AsyncServiceProvider.GlobalProvider, outputConsoleLogger)
        { }

        internal DefaultVSCredentialServiceProvider(
            IAsyncServiceProvider asyncServiceProvider,
            Lazy<INuGetUILogger> outputConsoleLogger
            )
        {
            _asyncServiceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
            _outputConsoleLogger = outputConsoleLogger ?? throw new ArgumentNullException(nameof(outputConsoleLogger));
        }

        public async Task<NuGet.Configuration.ICredentialService> GetCredentialServiceAsync()
        {
            // Initialize the credential providers.
            var credentialProviders = new List<ICredentialProvider>();
            var webProxy = await _asyncServiceProvider.GetServiceAsync<SVsWebProxy, IVsWebProxy>();

            TryAddCredentialProviders(
                credentialProviders,
                Strings.CredentialProviderFailed_VisualStudioAccountProvider,
                () =>
                {
                    var importer = new VsCredentialProviderImporter(
                        (exception, failureMessage) => LogCredentialProviderError(exception, failureMessage));

                    return importer.GetProviders();
                });

            TryAddCredentialProviders(
                credentialProviders,
                Strings.CredentialProviderFailed_VisualStudioCredentialProvider,
                () =>
                {
                    Debug.Assert(webProxy != null);

                    return new ICredentialProvider[] {
                        new VisualStudioCredentialProvider(webProxy)
                    };
                });

            await TryAddCredentialProvidersAsync(
                credentialProviders,
                Strings.CredentialProviderFailed_PluginCredentialProvider,
                async () => await (new SecurePluginCredentialProviderBuilder(PluginManager.Instance, canShowDialog: true, logger: NullLogger.Instance).BuildAllAsync())
                );

            if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
            {
                TryAddCredentialProviders(
                credentialProviders,
                Strings.CredentialProviderFailed_DefaultCredentialsCredentialProvider,
                () =>
                {
                    return new ICredentialProvider[] {
                        new DefaultNetworkCredentialsCredentialProvider()
                    };
                });
            }

            // Initialize the credential service.
            var credentialService = new CredentialService(new AsyncLazy<IEnumerable<ICredentialProvider>>(() => System.Threading.Tasks.Task.FromResult((IEnumerable<ICredentialProvider>)credentialProviders)), nonInteractive: false, handlesDefaultCredentials: PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders);

            return credentialService;
        }

        private async System.Threading.Tasks.Task TryAddCredentialProvidersAsync(
            List<ICredentialProvider> credentialProviders,
            string failureMessage,
            Func<Task<IEnumerable<ICredentialProvider>>> factory)
        {
            try
            {
                foreach (var credentialProvider in await factory())
                {
                    credentialProviders.Add(credentialProvider);
                }
            }
            catch (Exception exception)
            {
                LogCredentialProviderError(exception, failureMessage);
            }
        }

        private void TryAddCredentialProviders(
            List<ICredentialProvider> credentialProviders,
            string failureMessage,
            Func<IEnumerable<ICredentialProvider>> factory)
        {
            try
            {
                var providers = factory();

                if (providers != null)
                {
                    foreach (var credentialProvider in providers)
                    {
                        credentialProviders.Add(credentialProvider);
                    }
                }
            }
            catch (Exception exception)
            {
                LogCredentialProviderError(exception, failureMessage);
            }
        }

        private void LogCredentialProviderError(Exception exception, string failureMessage)
        {
            // Log the user-friendly message to the output console (no stack trace).
            _outputConsoleLogger.Value.Log(
                MessageLevel.Error,
                failureMessage +
                Environment.NewLine +
                ExceptionUtilities.DisplayMessage(exception));

            // Write the stack trace to the activity log.
            ActivityLog.LogWarning(
                ExceptionHelper.LogEntrySource,
                failureMessage +
                Environment.NewLine +
                exception);
        }
    }
}