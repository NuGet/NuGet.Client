// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Credentials
{
    public interface ICredentialProvider
    {
        string Id { get; }

        Task<CredentialResponse> Get(Uri uri, 
                               IWebProxy proxy, 
                               bool isProxyRequest, 
                               bool isRetry,
                               bool nonInteractive, 
                               CancellationToken cancellationToken);
    }
}
