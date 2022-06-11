// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    public static class HttpSourceUsage
    {
        public delegate void HttpSourceHitCacheEventHandler();
        public static event HttpSourceHitCacheEventHandler HttpSourceHitCacheEvent;

        public delegate void HttpSourceMissCacheEventHandler(bool cacheByPass, bool isExpired);
        public static event HttpSourceMissCacheEventHandler HttpSourceMissCacheEvent;

        public static void RaiseHttpSourceHitCacheEvent()
        {
            HttpSourceHitCacheEvent?.Invoke();
        }

        public static void RaiseHttpSourceMissCacheEvent(bool cacheByPass, bool isExpired)
        {
            HttpSourceMissCacheEvent?.Invoke(cacheByPass, isExpired);
        }
    }
}
