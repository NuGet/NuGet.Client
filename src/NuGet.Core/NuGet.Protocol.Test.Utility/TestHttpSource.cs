// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    /// <summary>
    /// HttpSource with file caching disabled
    /// </summary>
    public class TestHttpSource : HttpSource
    {
        public TestHttpSource(PackageSource source, Dictionary<string, string> responses, string errorContent = "") : base(
            source,
            () => Task.FromResult<HttpHandlerResource>(
                    new TestHttpHandler(
                        new TestMessageHandler(responses, errorContent))))
        {
        }

        public TestHttpSource(PackageSource source, Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses) : base(
            source,
            () => Task.FromResult<HttpHandlerResource>(
                    new TestHttpHandler(new TestMessageHandler(responses))))
        {
        }

        protected override Stream TryReadCacheFile(string uri, TimeSpan maxAge, string cacheFile)
        {
            return null;
        }
    }
}
