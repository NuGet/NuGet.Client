// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Credentials
{
    /// <summary>
    /// Wraps a v2 NuGet.ICredentialProvider to match the newer NuGet.Credentials.ICredentialProvider interface
    /// </summary>
    public class CredentialProviderAdapter : ICredentialProvider
    {
        private readonly NuGet.ICredentialProvider _provider;

        public CredentialProviderAdapter(NuGet.ICredentialProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _provider = provider;
        }

        public Task<ICredentials> Get(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var task = Task.FromResult(_provider.GetCredentials(
                uri,
                proxy,
                isProxyRequest ? CredentialType.ProxyCredentials : CredentialType.RequestCredentials,
                isRetry));

            return task;
        }
    }
}
