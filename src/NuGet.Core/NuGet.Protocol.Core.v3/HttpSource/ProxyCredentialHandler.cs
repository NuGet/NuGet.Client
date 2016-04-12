// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// A message handler responsible for retrying request for authenticated proxies
    /// with missing credentials.
    /// </summary>
    public class ProxyCredentialHandler : DelegatingHandler
    {
        public static readonly int MaxAuthRetries = 3;
        private const string BasicAuthenticationType = "Basic";

        // Only one source may prompt at a time
        private static readonly SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);

        private readonly HttpClientHandler _clientHandler;
        private readonly ICredentialService _credentialService;
        private readonly IProxyCredentialCache _credentialCache;

        private int _authRetries;

        public ProxyCredentialHandler(
            HttpClientHandler clientHandler,
            ICredentialService credentialService,
            IProxyCredentialCache credentialCache)
            : base(clientHandler)
        {
            if (clientHandler == null)
            {
                throw new ArgumentNullException(nameof(clientHandler));
            }

            _clientHandler = clientHandler;

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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                // Store the auth start before sending the request
                var cacheVersion = _credentialCache.Version;

                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    if (response.StatusCode != HttpStatusCode.ProxyAuthenticationRequired)
                    {
                        return response;
                    }

                    if (_clientHandler.Proxy == null)
                    {
                        return response;
                    }

                    if (!await AcquireCredentialsAsync(request.RequestUri, cacheVersion, cancellationToken))
                    {
                        return response;
                    }
                }
                catch (HttpRequestException ex) when (ProxyAuthenticationRequired(ex) && _clientHandler.Proxy != null)
                {
                    if (!await AcquireCredentialsAsync(request.RequestUri, cacheVersion, cancellationToken))
                    {
                        throw;
                    }
                }
            }
        }

#if !IS_CORECLR
        // Returns true if the cause of the exception is proxy authentication failure
        private static bool ProxyAuthenticationRequired(Exception ex)
        {
            var response = ExtractResponse(ex);
            return response?.StatusCode == HttpStatusCode.ProxyAuthenticationRequired;
        }

        private static HttpWebResponse ExtractResponse(Exception ex)
        {
            var webException = ex.InnerException as WebException;
            var response = webException?.Response as HttpWebResponse;
            return response;
        }
#else
        private static bool ProxyAuthenticationRequired(Exception ex)
        {
            return false;
        }
#endif

        private async Task<bool> AcquireCredentialsAsync(Uri requestUri, Guid cacheVersion, CancellationToken cancellationToken)
        {
            try
            {
                await _credentialPromptLock.WaitAsync();

                // Check if the credentials have already changed
                if (cacheVersion != _credentialCache.Version)
                {
                    // retry the request with updated credentials
                    return true;
                }

                // Limit the number of retries
                _authRetries++;
                if (_authRetries >= MaxAuthRetries)
                {
                    // user prompting no more
                    return false;
                }

                var proxyAddress = _clientHandler.Proxy.GetProxy(requestUri);

                // prompt user for proxy credentials.
                var credentials = await PromptForProxyCredentialsAsync(proxyAddress, _clientHandler.Proxy, cancellationToken);

                if (credentials == null)
                {
                    // user cancelled
                    return false;
                }

                _credentialCache.UpdateCredential(proxyAddress, credentials);

                // use the user provided credential to send the request again.
                return true;
            }
            finally
            {
                _credentialPromptLock.Release();
            }
        }

        private async Task<NetworkCredential> PromptForProxyCredentialsAsync(Uri proxyAddress, IWebProxy proxy, CancellationToken cancellationToken)
        {
            var message = string.Format(
                CultureInfo.CurrentCulture,
                Strings.Http_CredentialsForProxy,
                proxyAddress);

            var credentials = await _credentialService.GetCredentialsAsync(
                proxyAddress,
                proxy,
                type: CredentialRequestType.Proxy,
                message: message,
                cancellationToken: cancellationToken);

            return credentials?.GetCredential(proxyAddress, BasicAuthenticationType);
        }
    }
}