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
    public class FileSystemHttpCacheUtility : IHttpCacheUtility
    {
        public static FileSystemHttpCacheUtility Instance { get; } = new FileSystemHttpCacheUtility();

        public HttpCacheResult InitializeHttpCacheResult(string httpCacheDirectory, Uri sourceUri, string cacheKey, HttpSourceCacheContext context)
        {
            return HttpCacheUtility.InitializeHttpCacheResult(
                httpCacheDirectory,
                sourceUri,
                cacheKey,
                context);
        }

        public Task<Stream> TryReadCacheFileAsync(TimeSpan maxAge, string cacheFile)
        {
            return Task.FromResult(CachingUtility.ReadCacheFile(maxAge, cacheFile));
        }

        public async Task CreateCacheFileAsync(
            HttpCacheResult result,
            HttpResponseMessage response,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken)
        {
            await HttpCacheUtility.CreateCacheFileAsync(
                result,
                response,
                ensureValidContents,
                cancellationToken);
        }
    }
}
