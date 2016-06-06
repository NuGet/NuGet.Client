// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    public class HttpSourceAuthenticationHandler : DelegatingHandler
    {
        public static readonly int MaxAuthRetries = AmbientAuthenticationState.MaxAuthRetries;

        // Only one source may prompt at a time
        private readonly static SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);

        private readonly PackageSource _packageSource;
        private readonly HttpClientHandler _clientHandler;
        private readonly ICredentialService _credentialService;

        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);
        private Dictionary<string, AmbientAuthenticationState> _authStates = new Dictionary<string, AmbientAuthenticationState>();
        private HttpSourceCredentials _credentials;

        public HttpSourceAuthenticationHandler(
            PackageSource packageSource,
            HttpClientHandler clientHandler,
            ICredentialService credentialService)
            : base(clientHandler)
        {
            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            _packageSource = packageSource;

            if (clientHandler == null)
            {
                throw new ArgumentNullException(nameof(clientHandler));
            }

            _clientHandler = clientHandler;

            // credential service is optional as credentials may be attached to a package source
            _credentialService = credentialService;

            // Create a new wrapper for ICredentials that can be modified
            _credentials = new HttpSourceCredentials();

            if (packageSource.Credentials != null &&
                packageSource.Credentials.IsValid())
            {
                var credentials = new NetworkCredential(packageSource.Credentials.Username, packageSource.Credentials.Password);
                _credentials.Credentials = credentials;
            }

            _clientHandler.Credentials = _credentials;
            // Always take the credentials from the helper.
            _clientHandler.UseDefaultCredentials = false;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            ICredentials promptCredentials = null;

            var configuration = request.GetOrCreateConfiguration();

            // Authorizing may take multiple attempts
            while (true)
            {
                // Clean up any previous responses
                if (response != null)
                {
                    response.Dispose();
                }

                // store the auth state before sending the request
                var beforeLockVersion = _credentials.Version;

                response = await base.SendAsync(request, cancellationToken);

                if (_credentialService == null)
                {
                    return response;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    (configuration.PromptOn403 && response.StatusCode == HttpStatusCode.Forbidden))
                {
                    promptCredentials = await AcquireCredentialsAsync(
                        response.StatusCode,
                        beforeLockVersion,
                        configuration.Logger,
                        cancellationToken);

                    if (promptCredentials == null)
                    {
                        return response;
                    }

                    continue;
                }

                if (promptCredentials != null)
                {
                    CredentialsSuccessfullyUsed(_packageSource.SourceUri, promptCredentials);
                }

                return response;
            }
        }

        private async Task<ICredentials> AcquireCredentialsAsync(HttpStatusCode statusCode, Guid credentialsVersion, ILogger log, CancellationToken cancellationToken)
        {
            try
            {
                // Only one request may prompt and attempt to auth at a time
                await _httpClientLock.WaitAsync();

                cancellationToken.ThrowIfCancellationRequested();

                // Auth may have happened on another thread, if so just continue
                if (credentialsVersion != _credentials.Version)
                {
                    return _credentials.Credentials;
                }

                var authState = GetAuthenticationState();

                authState.Increment();

                if (authState.IsBlocked)
                {
                    return null;
                }

                // Prompt the user
                CredentialRequestType type;
                string message;
                if (statusCode == HttpStatusCode.Unauthorized)
                {
                    type = CredentialRequestType.Unauthorized;
                    message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Http_CredentialsForUnauthorized,
                        _packageSource.Source);
                }
                else
                {
                    type = CredentialRequestType.Forbidden;
                    message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Http_CredentialsForForbidden,
                        _packageSource.Source);
                }

                var promptCredentials = await PromptForCredentialsAsync(
                    type,
                    message,
                    log,
                    cancellationToken);

                if (promptCredentials == null)
                {
                    // null means cancelled by user or error occured
                    // block subsequent attempts to annoy user with prompts
                    authState.IsBlocked = true;

                    cancellationToken.ThrowIfCancellationRequested();

                    return null;
                }

                _credentials.Credentials = promptCredentials;

                return promptCredentials;
            }
            finally
            {
                _httpClientLock.Release();
            }
        }

        private AmbientAuthenticationState GetAuthenticationState()
        {
            var correlationId = ActivityCorrelationContext.Current.CorrelationId;

            AmbientAuthenticationState authState;
            if (!_authStates.TryGetValue(correlationId, out authState))
            {
                authState = new AmbientAuthenticationState();
                _authStates[correlationId] = authState;
            }

            return authState;
        }

        private async Task<ICredentials> PromptForCredentialsAsync(
            CredentialRequestType type,
            string message,
            ILogger log,
            CancellationToken token)
        {
            ICredentials promptCredentials;

            try
            {
                // Only one prompt may display at a time.
                await _credentialPromptLock.WaitAsync();

                // Get the proxy for this URI so we can pass it to the credentialService methods
                // this lets them use the proxy if they have to hit the network.
                var proxyCache = ProxyCache.Instance;
                var proxy = proxyCache?.GetProxy(_packageSource.SourceUri);

                promptCredentials = await _credentialService
                    .GetCredentialsAsync(_packageSource.SourceUri, proxy, type, message, token);
            }
            catch (TaskCanceledException)
            {
                throw; // pass-thru
            }
            catch (OperationCanceledException)
            {
                // A valid response for VS dialog when user hits cancel button
                promptCredentials = null;
            }
            catch (Exception e)
            {
                // Fatal credential service failure
                log.LogError(ExceptionUtilities.DisplayMessage(e));
                promptCredentials = null;
            }
            finally
            {
                _credentialPromptLock.Release();
            }

            return promptCredentials;
        }

        private void CredentialsSuccessfullyUsed(Uri uri, ICredentials credentials)
        {
            HttpHandlerResourceV3.CredentialsSuccessfullyUsed?.Invoke(uri, credentials);
        }
    }
}