// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    /// <summary>
    /// A mutable CredentialCache wrapper. This allows the underlying ICredentials to
    /// be changed to work around HttpClientHandler not allowing Credentials to change.
    /// This class intentionally inherits from CredentialCache to support authentication on redirects.
    /// According to System.Net implementation any other ICredentials implementation is dropped for security reasons.
    /// </summary>
    public class HttpSourceCredentials : CredentialCache, ICredentials
    {
        /// <summary>
        /// Credentials can be changed by other threads, for this reason volatile
        /// is added below so that the value is not cached anywhere.
        /// </summary>
        private volatile ICredentials _credentials;

        /// <summary>
        /// Latest credentials to be used.
        /// </summary>
        public ICredentials Credentials
        {
            get
            {
                return _credentials;
            }

            set
            {
                Version = Guid.NewGuid();
                _credentials = value;
            }
        }

        public Guid Version { get; private set; } = Guid.NewGuid();

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSourceCredentials"/> class
        /// </summary>
        /// <param name="credentialService">
        /// The credential service that will be used to handle authentications failures. May be null.
        /// </param>
        public HttpSourceCredentials(ICredentialService credentialService)
        {
            if (credentialService == null || !credentialService.HandlesDefaultCredentials)
            {
                // This is used to match the value of HttpClientHandler.UseDefaultCredentials = true
                _credentials = DefaultNetworkCredentials;
            }
        }

        /// <summary>
        /// Used by the HttpClientHandler to retrieve the current credentials.
        /// </summary>
        NetworkCredential ICredentials.GetCredential(Uri uri, string authType)
        {
            // Get credentials from the current credential store, if any
            return Credentials?.GetCredential(uri, authType);
        }
    }
}
