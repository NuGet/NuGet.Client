// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.Credentials
{
    public sealed class SecurePluginCredentialProvider : ICredentialProvider
    {
        /// <summary>
        /// Plugin that this provider will use to acquire credentials
        /// </summary>
        private readonly PluginDiscoveryResult _discoveredPlugin;

        /// <summary>
        /// logger
        /// </summary>
        private readonly Common.ILogger _logger;

        /// <summary>
        /// pluginManager
        /// </summary>
        private readonly IPluginManager _pluginManager;

        // We use this to avoid needlessly instantiating plugins if they don't support authentication.
        private bool _isAnAuthenticationPlugin = true;

        /// <summary>
        /// Create a credential provider based on provided plugin
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <param name="pluginDiscoveryResult"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException">if <paramref name="pluginDiscoveryResult"/> is null</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="logger"/> is null</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="pluginManager"/> is null</exception>
        /// <exception cref="ArgumentException">if plugin file is not valid</exception>
        public SecurePluginCredentialProvider(IPluginManager pluginManager, PluginDiscoveryResult pluginDiscoveryResult, Common.ILogger logger)
        {
            if (pluginDiscoveryResult == null)
            {
                throw new ArgumentNullException(nameof(pluginDiscoveryResult));
            }
            if (pluginDiscoveryResult.PluginFile.State != PluginFileState.Valid)
            {
                throw new ArgumentException(string.Format(Resources.SecureCredentialProvider_InvalidPluginFile, pluginDiscoveryResult.PluginFile.State, pluginDiscoveryResult.Message), nameof(pluginDiscoveryResult));
            }
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _discoveredPlugin = pluginDiscoveryResult;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Id = $"{nameof(SecurePluginCredentialProvider)}_{pluginDiscoveryResult.PluginFile.Path}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        /// <param name="uri">The uri of a web resource for which credentials are needed.</param>
        /// <param name="proxy">Ignored.  Proxy information will not be passed to plugins.</param>
        /// <param name="type">
        /// The type of credential request that is being made. Note that this implementation of
        /// <see cref="ICredentialProvider"/> does not support providing proxy credenitials and treats
        /// all other types the same.
        /// </param>
        /// <param name="isRetry">If true, credentials were previously supplied by this
        /// provider for the same uri.</param>
        /// <param name="message">A message provided by NuGet to show to the user when prompting.</param>
        /// <param name="nonInteractive">If true, the plugin must not prompt for credentials.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A credential object.</returns>
        public async Task<CredentialResponse> GetAsync(Uri uri, IWebProxy proxy, CredentialRequestType type, string message, bool isRetry, bool nonInteractive, CancellationToken cancellationToken)
        {
            CredentialResponse taskResponse = null;
            if (type == CredentialRequestType.Proxy || !_isAnAuthenticationPlugin)
            {
                taskResponse = new CredentialResponse(CredentialStatus.ProviderNotApplicable);
                return taskResponse;
            }

            var plugin = await _pluginManager.CreateSourceAgnosticPluginAsync(_discoveredPlugin, cancellationToken);

            _isAnAuthenticationPlugin = plugin.Claims.Contains(OperationClaim.Authentication);

            if (_isAnAuthenticationPlugin)
            {
                var request = new GetAuthenticationCredentialsRequest(uri, isRetry, nonInteractive);

                var credentialResponse = await plugin.Plugin.Connection.SendRequestAndReceiveResponseAsync<GetAuthenticationCredentialsRequest, GetAuthenticationCredentialsResponse>(
                    MessageMethod.GetAuthenticationCredentials,
                    request,
                    cancellationToken);
                taskResponse = GetAuthenticationCredentialsResponseToCredentialResponse(credentialResponse);
            }
            else
            {
                taskResponse = new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            }

            return taskResponse;
        }

        /// <summary>
        /// Convert from Plugin CredentialResponse to the CredentialResponse model used by the ICredentialService
        /// </summary>
        /// <param name="credentialResponse"></param>
        /// <returns>credential response</returns>
        private static CredentialResponse GetAuthenticationCredentialsResponseToCredentialResponse(GetAuthenticationCredentialsResponse credentialResponse)
        {
            CredentialResponse taskResponse;
            if (credentialResponse.IsValid)
            {
                ICredentials result = new NetworkCredential(credentialResponse.Username, credentialResponse.Password);
                if (credentialResponse.AuthenticationTypes != null)
                {
                    result = new AuthTypeFilteredCredentials(result, credentialResponse.AuthenticationTypes);
                }

                taskResponse = new CredentialResponse(result);
            }
            else
            {
                taskResponse = new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            }

            return taskResponse;
        }
    }
}
