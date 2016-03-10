// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.v3
{
    internal class ProxyCredentialDriver : IProxyCredentialDriver
    {
        private const int MaxAuthRetries = 3;
        private const string BasicAuthenticationType = "Basic";

        // Only one source may prompt at a time
        private readonly static SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);

        private readonly ICredentialService _credentialService;
        private readonly ProxyCredentialCache _credentialCache;

        private int _authRetries;

        public ProxyCredentialDriver(ICredentialService credentialService, ProxyCredentialCache credentialCache)
        {
            if (credentialService == null)
            {
                throw new ArgumentNullException(nameof(credentialService));
            }

            _credentialService = credentialService;

            if (credentialCache == null)
            {
                throw new ArgumentNullException(nameof(credentialCache));
            }

            _credentialCache = credentialCache;
        }

        public async Task<NetworkCredential> AcquireCredentialsAsync(Uri proxyAddress, IWebProxy proxy, CancellationToken cancellationToken)
        {
            // Store the auth start before sending the request
            var cacheVersion = _credentialCache.Version;

            try
            {
                await _credentialPromptLock.WaitAsync();

                // Check if the credentials have already changed
                if (cacheVersion != _credentialCache.Version)
                {
                    return _credentialCache.GetCredential(proxyAddress, BasicAuthenticationType);
                }

                // Limit the number of retries
                _authRetries++;
                if (_authRetries >= MaxAuthRetries)
                {
                    // user prompting no more
                    return null;
                }

                // prompt user for proxy credentials.
                var credentials = await PromptForProxyCredentialsAsync(proxyAddress, proxy, cancellationToken);

                if (credentials == null)
                {
                    return null;
                }

                _credentialCache.Add(proxyAddress, credentials);

                // use the user provided credential to send the request again.
                return credentials;
            }
            finally
            {
                _credentialPromptLock.Release();
            }
        }

        private async Task<NetworkCredential> PromptForProxyCredentialsAsync(Uri proxyAddress, IWebProxy proxy, CancellationToken cancellationToken)
        {
            var credentials = await _credentialService.GetCredentials(
                proxyAddress, proxy, isProxy: true, cancellationToken: cancellationToken);
            return credentials.GetCredential(proxyAddress, BasicAuthenticationType);
        }
    }
}
