// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol
{
    public interface IHttpRetryHandler
    {
        Task<HttpResponseMessage> SendAsync(
            HttpRetryHandlerRequest request,
            ILogger log,
            CancellationToken cancellationToken);
    }
}