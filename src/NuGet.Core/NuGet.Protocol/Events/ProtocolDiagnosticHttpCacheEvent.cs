// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Events
{
#pragma warning disable RS0016 // Add public types and members to the declared API
    public sealed class ProtocolDiagnosticHttpCacheEvent
    {
        public string Source { get; }
        public bool CacheHit { get; }
        public bool CacheBypass { get; }
        public bool ExpiredCache { get; }
        public bool CacheFileHashMatch { get; }
        public string Request { get; }

        public ProtocolDiagnosticHttpCacheEvent(
#pragma warning restore RS0016 // Add public types and members to the declared API
            string source,
            string request,
            bool cacheHit,
            bool cacheBypass,
            bool expiredCache,
            bool cacheFileHashMatch)
        {
            Source = source;
            Request = request;
            CacheHit = cacheHit;
            CacheBypass = cacheBypass;
            ExpiredCache = expiredCache;
            CacheFileHashMatch = cacheFileHashMatch;
        }
    }
}
