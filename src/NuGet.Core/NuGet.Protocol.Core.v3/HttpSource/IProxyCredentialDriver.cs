// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Represents a flow of acquiring user credentials. Design to support interactive scenarios.
    /// </summary>
    public interface IProxyCredentialDriver
    {
        Task<NetworkCredential> AcquireCredentialsAsync(Uri proxyAddress, IWebProxy proxy, CancellationToken cancellationToken);
    }
}
