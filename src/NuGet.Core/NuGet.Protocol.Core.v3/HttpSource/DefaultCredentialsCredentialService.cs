// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    /// <summary>
    /// A credential service which always returns <see cref="CredentialCache.DefaultNetworkCredentials" />
    /// </summary>
    /// <remarks>
    /// Used as a default to preserve default-credentials behavior for clients which do not supply a
    /// credential service, when the flag is enabled to delay using default credentials until after
    /// the plugin credential providers run.
    /// </remarks>
    public class DefaultCredentialsCredentialService : ICredentialService
    {
        /// <summary>
        /// Returns a task resulting in <see cref="CredentialCache.DefaultNetworkCredentials" />
        /// </summary>
        public Task<ICredentials> GetCredentialsAsync(
            Uri uri,
            IWebProxy proxy,
            CredentialRequestType type,
            string message,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ICredentials>(CredentialCache.DefaultNetworkCredentials);
        }
    }
}