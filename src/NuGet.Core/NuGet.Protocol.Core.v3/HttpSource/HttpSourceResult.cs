// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol
{
    public class HttpSourceResult : IDisposable
    {
        public Stream Stream { get; private set; }
        public HttpSourceResultStatus Status { get; }
        public string CacheFileName { get; }
        
        public HttpSourceResult(HttpSourceResultStatus status)
        {
            Status = status;
            Stream = null;
            CacheFileName = null;
        }

        public HttpSourceResult(HttpSourceResultStatus status, string cacheFileName, Stream stream)
        {
            Status = status;
            Stream = stream;
            CacheFileName = cacheFileName;
        }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.Dispose();
                Stream = null;
            }
        }
    }
}