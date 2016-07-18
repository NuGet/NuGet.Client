// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol
{
    public class HttpCacheResult
    {
        public HttpCacheResult(TimeSpan maxAge, string newCacheFile, string cacheFile)
        {
            MaxAge = maxAge;
            NewCacheFile = newCacheFile;
            CacheFile = cacheFile;
        }

        public TimeSpan MaxAge { get; }
        public string NewCacheFile { get; }
        public string CacheFile { get; }
        public Stream Stream { get; set; }
    }
}
