// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Events
{
#pragma warning disable RS0016 // Add public types and members to the declared API
    public sealed class ProtocolDiagnosticHttpCacheEvent
    {
        public string Source { get; }
        public string Request { get; }
        public bool DirectDownload { get; }
        public bool IfModifiedHeaderSinceSent { get; }
        public bool CacheHit { get; }
        public bool CacheBypass { get; }
        public bool CacheFileReused { get; }
        public bool? ExpiredCache { get; }
        public bool? CacheFileNotModified { get; }

        public ProtocolDiagnosticHttpCacheEvent(
#pragma warning restore RS0016 // Add public types and members to the declared API
            string source,
            string request,
            bool directDownload,
            bool ifModifiedSinceHeaderSent,
            bool cacheHit,
            bool cacheBypass,
            bool cacheFileReused,
            bool? expiredCache,
            bool? cacheFileNotModified)
        {
            Source = source;
            Request = request;
            DirectDownload = directDownload;
            IfModifiedHeaderSinceSent = ifModifiedSinceHeaderSent;
            CacheHit = cacheHit;
            CacheBypass = cacheBypass;
            CacheFileReused = cacheFileReused;
            ExpiredCache = expiredCache;
            CacheFileNotModified = cacheFileNotModified;
        }
    }
}
