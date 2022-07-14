// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Wraps an IVsCredentialProvider.  IVsCredentialProvider ensures that VS Extensions 
    /// can supply credential providers implementing a stable interface across versions.
    /// </summary>
    public class VsCredentialProviderAdapter : ICredentialProvider
    {
        private readonly IVsCredentialProvider _provider;

        public VsCredentialProviderAdapter(IVsCredentialProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _provider = provider;
        }

        public string Id => _provider.GetType().FullName;

        public async Task<CredentialResponse> GetAsync(
            Uri uri,
            IWebProxy proxy,
            CredentialRequestType type,
            string message,
            bool isRetry,
            bool nonInteractive,
            CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            // TODO: Extend the IVS API surface area to pass down the credential request type.

            // Telling the credential provider to cancel the request in situations like PM UI being closed or the search query changing
            // is disruptive, because it may prompt the customer for interactive input, but then not get/cache a token, so the next request
            // has to prompt the customer again. Therefore, let's only tell the provider to cancel when VS is shutting down, so it has
            // the opportunity to cache tokens and not need to prompt the customer in the near future.
            Task<ICredentials> task = _provider.GetCredentialsAsync(
                uri,
                proxy,
                isProxyRequest: type == CredentialRequestType.Proxy,
                isRetry: isRetry,
                nonInteractive: nonInteractive,
                cancellationToken: VsShellUtilities.ShutdownToken);

            // Since the above task will only cancel when VS is shutting down, we should abandon the task when our own cancellation token
            // requests cancellation, so that scenarios like cancelled HTTP requests when PM UI is closed can free resources more quickly.
            ICredentials credentials = await task.WithCancellation(cancellationToken);

            return credentials == null
                ? new CredentialResponse(CredentialStatus.ProviderNotApplicable)
                : new CredentialResponse(credentials);
        }
    }
}
