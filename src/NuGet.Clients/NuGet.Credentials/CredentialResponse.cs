// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;

namespace NuGet.Credentials
{
    public class CredentialResponse
    {
        private CredentialResponse()
        {
        }

        /// <summary>
        /// Creates a Credential response object without giving credentials.
        /// Note should only be done if the status is ProviderNotApplicable.
        /// </summary>
        /// <param name="status"></param>
        public CredentialResponse(CredentialStatus status) : this(null, status)
        {
        }

        /// <summary>
        /// Crates a credential response object
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="status"></param>
        public CredentialResponse(ICredentials credentials, CredentialStatus status)
        {
            if ((credentials != null && status == CredentialStatus.ProviderNotApplicable) ||
                (credentials == null && status == CredentialStatus.Success))
            {
                throw new ProviderException(Resources.ProviderException_InvalidCredentialResponse);
            }

            Credentials = credentials;
            Status = status;
        }

        public ICredentials Credentials { get; }
        public CredentialStatus Status { get; }
    }

    public enum CredentialStatus
    {
        Success,
        ProviderNotApplicable
    }
}
