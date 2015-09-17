// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Net.Http;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Simulated response from the cache.
    /// </summary>
    public sealed class CacheResponse : HttpResponseMessage
    {
        public CacheResponse(Stream stream)
            : base(HttpStatusCode.OK)
        {
            Headers.Add("X-NuGet-FileCache", "true");
            Content = new StreamContent(stream);
        }
    }
}
