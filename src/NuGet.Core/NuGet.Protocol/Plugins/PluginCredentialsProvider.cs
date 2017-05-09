// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A credentials provider for plugins.
    /// </summary>
    public sealed class PluginCredentialsProvider : IRequestHandler, IDisposable
    {
        private const string _basicAuthenticationType = "Basic";

        private readonly ICredentialService _credentialService;
        private bool _isDisposed;
        private readonly PackageSource _packageSource;
        private readonly IPlugin _plugin;
        private readonly IWebProxy _proxy;

        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        public CancellationToken CancellationToken => CancellationToken.None;

        /// <summary>
        /// Initializes a new <see cref="PluginCredentialsProvider" /> class.
        /// </summary>
        /// <param name="plugin">A plugin.</param>
        /// <param name="packageSource">A package source.</param>
        /// <param name="proxy">A web proxy.</param>
        /// <param name="credentialService">A credential service.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plugin" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageSource" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="credentialService" />
        /// is <c>null</c>.</exception>
        public PluginCredentialsProvider(
            IPlugin plugin,
            PackageSource packageSource,
            IWebProxy proxy,
            ICredentialService credentialService)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            if (credentialService == null)
            {
                throw new ArgumentNullException(nameof(credentialService));
            }

            _plugin = plugin;
            _packageSource = packageSource;
            _proxy = proxy;
            _credentialService = credentialService;
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _plugin.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Asynchronously handles cancelling a request.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="request">A request message.</param>
        /// <param name="responseHandler">A response handler.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public Task HandleCancelAsync(
            IConnection connection,
            Message request,
            IResponseHandler responseHandler,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Asynchronously handles responding to a request.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="request">A request message.</param>
        /// <param name="responseHandler">A response handler.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="responseHandler" />
        /// is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public async Task HandleResponseAsync(
            IConnection connection,
            Message request,
            IResponseHandler responseHandler,
            CancellationToken cancellationToken)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (responseHandler == null)
            {
                throw new ArgumentNullException(nameof(responseHandler));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var requestPayload = MessageUtilities.DeserializePayload<GetCredentialsRequest>(request);
            GetCredentialsResponse responsePayload;

            if (_packageSource.IsHttp &&
                string.Equals(
                    requestPayload.PackageSourceRepository,
                    _packageSource.Source,
                    StringComparison.OrdinalIgnoreCase))
            {
                NetworkCredential credential = null;

                using (var progressReporter = AutomaticProgressReporter.Create(
                    _plugin,
                    request,
                    PluginConstants.ProgressInterval,
                    cancellationToken))
                {
                    credential = await GetCredentialAsync(requestPayload.StatusCode, cancellationToken);
                }

                if (credential == null)
                {
                    responsePayload = new GetCredentialsResponse(
                        MessageResponseCode.NotFound,
                        username: null,
                        password: null);
                }
                else
                {
                    responsePayload = new GetCredentialsResponse(
                        MessageResponseCode.Success,
                        credential.UserName,
                        credential.Password);
                }
            }
            else
            {
                responsePayload = new GetCredentialsResponse(
                    MessageResponseCode.NotFound,
                    username: null,
                    password: null);
            }

            await responseHandler.SendResponseAsync(request, responsePayload, cancellationToken);
        }

        private async Task<NetworkCredential> GetCredentialAsync(
            HttpStatusCode statusCode,
            CancellationToken cancellationToken)
        {
            var requestType = GetCredentialRequestType(statusCode);

            if (requestType == CredentialRequestType.Proxy)
            {
                return await GetProxyCredentialAsync(cancellationToken);
            }

            return await GetPackageSourceCredential(requestType, cancellationToken);
        }

        private async Task<NetworkCredential> GetPackageSourceCredential(
            CredentialRequestType requestType,
            CancellationToken cancellationToken)
        {
            if (_packageSource.Credentials != null && _packageSource.Credentials.IsValid())
            {
                return new NetworkCredential(_packageSource.Credentials.Username, _packageSource.Credentials.Password);
            }

            string message;
            if (requestType == CredentialRequestType.Unauthorized)
            {
                message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Http_CredentialsForUnauthorized,
                    _packageSource.Source);
            }
            else
            {
                message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Http_CredentialsForForbidden,
                    _packageSource.Source);
            }

            var sourceUri = _packageSource.SourceUri;
            var credentials = await _credentialService.GetCredentialsAsync(
                sourceUri,
                _proxy,
                requestType,
                message,
                cancellationToken);

            return credentials.GetCredential(sourceUri, authType: null);
        }

        private async Task<NetworkCredential> GetProxyCredentialAsync(CancellationToken cancellationToken)
        {
            if (_proxy != null)
            {
                var sourceUri = _packageSource.SourceUri;
                var proxyUri = _proxy.GetProxy(sourceUri);
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Http_CredentialsForProxy,
                    proxyUri);
                var proxyCredentials = await _credentialService.GetCredentialsAsync(
                    sourceUri,
                    _proxy,
                    CredentialRequestType.Proxy,
                    message,
                    cancellationToken);

                return proxyCredentials?.GetCredential(proxyUri, _basicAuthenticationType);
            }

            return null;
        }

        private static CredentialRequestType GetCredentialRequestType(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.ProxyAuthenticationRequired:
                    return CredentialRequestType.Proxy;

                case HttpStatusCode.Unauthorized:
                    return CredentialRequestType.Unauthorized;

                case HttpStatusCode.Forbidden:
                default:
                    return CredentialRequestType.Forbidden;
            }
        }
    }
}