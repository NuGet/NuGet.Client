// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    public static class HttpCacheUsage
    {
        public delegate void HttpCacheHitEventHandler();
        public static event HttpCacheHitEventHandler HttpCacheHitEvent;

        public delegate void HttpCacheMissEventHandler(bool cacheByPass, bool isExpired);
        public static event HttpCacheMissEventHandler HttpCacheMissEvent;

        public static void RaiseHttpCacheHitEvent()
        {
            HttpCacheHitEvent?.Invoke();
        }

        public static void RaiseHttpMissCacheEvent(bool cacheByPass, bool isExpired)
        {
            HttpCacheMissEvent?.Invoke(cacheByPass, isExpired);
        }
    }
}
