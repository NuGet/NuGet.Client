﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
extern alias CoreV2;

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
        private readonly CoreV2.NuGet.ICredentialProvider _provider;

        public CredentialProviderAdapter(CoreV2.NuGet.ICredentialProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _provider = provider;
            Id = $"{typeof(CredentialProviderAdapter).Name}_{provider.GetType().Name}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        public Task<CredentialResponse> GetAsync(
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

            var cred = _provider.GetCredentials(
                uri,
                proxy,
                type == CredentialRequestType.Proxy ? CoreV2.NuGet.CredentialType.ProxyCredentials : CoreV2.NuGet.CredentialType.RequestCredentials,
                isRetry);

            var response = cred != null
                ? new CredentialResponse(cred)
                : new CredentialResponse(CredentialStatus.ProviderNotApplicable);

            return Task.FromResult(response);
        }
    }
}
