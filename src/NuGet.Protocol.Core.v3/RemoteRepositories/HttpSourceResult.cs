// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    internal class HttpSourceResult : IDisposable
    {
        public string CacheFileName { get; set; }
        public Stream Stream { get; set; }

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
