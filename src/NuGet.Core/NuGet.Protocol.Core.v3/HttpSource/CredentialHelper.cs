// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;

namespace NuGet.Protocol
{
    /// <summary>
    /// A mutable CredentialCache wrapper. This allows the underlying ICredentials to
    /// be changed to work around HttpClientHandler not allowing Credentials to change.
    /// This class intentionally inherits from CredentialCache to support authentication on redirects.
    /// According to System.Net implementation any other ICredentials implementation is dropped for security reasons.
    /// </summary>
    public class CredentialHelper : CredentialCache, ICredentials
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
                _credentials = value;
            }
        }

        /// <summary>
        /// Used by the HttpClientHandler to retrieve the current credentials.
        /// </summary>
        NetworkCredential ICredentials.GetCredential(Uri uri, string authType)
        {
            // Credentials may change during this call so keep a local copy.
            var currentCredentials = Credentials;

            NetworkCredential result = null;

            if (currentCredentials == null)
            {
                // This is used to match the value of HttpClientHandler.UseDefaultCredentials = true
                result = CredentialCache.DefaultNetworkCredentials;
            }
            else
            {
                // Get credentials from the current credential store.
                result = currentCredentials.GetCredential(uri, authType);
            }

            return result;
        }
    }
}
