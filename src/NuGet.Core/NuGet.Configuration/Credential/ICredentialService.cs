// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Configuration
{
    public interface ICredentialService
    {
        Task<ICredentials> GetCredentialsAsync(
            Uri uri,
            IWebProxy proxy,
            CredentialRequestType type,
            string message,
            CancellationToken cancellationToken);
    }
}
