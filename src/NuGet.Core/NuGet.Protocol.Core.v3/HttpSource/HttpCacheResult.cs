// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Protocol
{
    public class HttpCacheResult
    {
        public string CacheFileName { get; set; }
        public Stream Stream { get; set; }
    }
}