// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public interface IHttpCacheUtility
    {
        HttpCacheResult InitializeHttpCacheResult(
            string httpCacheDirectory,
            Uri sourceUri,
            string cacheKey,
            HttpSourceCacheContext context);

        Task<Stream> TryReadCacheFileAsync(
            TimeSpan maxAge,
            string cacheFile);

        Task CreateCacheFileAsync(
            HttpCacheResult result,
            HttpResponseMessage response,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken);
    }
}
