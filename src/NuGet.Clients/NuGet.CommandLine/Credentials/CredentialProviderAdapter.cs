// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Credentials
{
    /// <summary>
    /// Wraps a v2 NuGet.ICredentialProvider to match the newer NuGet.Credentials.ICredentialProvider interface
    /// </summary>
    public class CredentialProviderAdapter : ICredentialProvider
    {
        //private readonly NuGet.ICredentialProvider _provider;
        private readonly ICredentialService _credentialService;

        public CredentialProviderAdapter(CredentialService credentialService) // TODO NK - Does this really make sense?
        {
            if (credentialService == null)
            {
                throw new ArgumentNullException(nameof(credentialService));
            }

            _credentialService = credentialService;
            Id = $"{typeof (CredentialProviderAdapter).Name}_{_credentialService.GetType().Name}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        public async Task<CredentialResponse> GetAsync( // TODO NK - REVIEW!
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

            cancellationToken.ThrowIfCancellationRequested();

            var credsTask = _credentialService.GetCredentialsAsync(uri, proxy, type,"TODO NK", cancellationToken);
            var creds = await credsTask;
            //var cred = _provider.GetCredentials(
            //    uri,
            //    proxy,
            //    type == CredentialRequestType.Proxy ? CredentialType.ProxyCredentials : CredentialType.RequestCredentials,
            //    isRetry);

            //var response = cred != null
            //    ? new CredentialResponse(cred)
            //    : new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            var response = creds != null
               ? new CredentialResponse(creds)
               : new CredentialResponse(CredentialStatus.ProviderNotApplicable);

            return response;
        }
    }
}
