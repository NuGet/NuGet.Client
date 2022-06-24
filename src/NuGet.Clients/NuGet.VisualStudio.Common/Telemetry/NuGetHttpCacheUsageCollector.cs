// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol.Events;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio.Common.Telemetry
{
    public sealed class NuGetHttpCacheUsageCollector : IDisposable
    {
        // Http cache hit/miss telemetry consts
        public const string HitCount = nameof(HitCount);
        public const string MissCount = nameof(MissCount);
        public const string CacheBypassCount = nameof(CacheBypassCount);
        public const string ExpiredCacheCount = nameof(ExpiredCacheCount);
        public const string HttpCache = "HttpCache.";

        private readonly InstanceData _vsInstanceData;
        private object _lock = new object();

        public NuGetHttpCacheUsageCollector()
        {
            _vsInstanceData = new InstanceData();
            ProtocolDiagnostics.HttpCacheHitEvent += HttpCacheUsage_CacheHitEventHandler;
            ProtocolDiagnostics.HttpCacheMissEvent += HttpCacheUsage_CacheMissEventHandler;
            InstanceCloseTelemetryEmitter.AddEventsOnShutdown += HttpCacheUsage_VSInstanceCloseHandler;
        }

        private void HttpCacheUsage_CacheHitEventHandler()
        {
            _vsInstanceData.IncrementCacheHitCount();
        }

        private void HttpCacheUsage_CacheMissEventHandler(bool cacheByPass, bool isExpired)
        {
            _vsInstanceData.IncrementCacheMissCount(cacheByPass, isExpired);
        }

        private void HttpCacheUsage_VSInstanceCloseHandler(object sender, TelemetryEvent telemetryEvent)
        {
            lock (_lock)
            {
                // Add VS Instance telemetry
                _vsInstanceData.AddProperties(telemetryEvent);
            }
        }

        public void Dispose()
        {
            ProtocolDiagnostics.HttpCacheHitEvent -= HttpCacheUsage_CacheHitEventHandler;
            ProtocolDiagnostics.HttpCacheMissEvent -= HttpCacheUsage_CacheMissEventHandler;
            InstanceCloseTelemetryEmitter.AddEventsOnShutdown -= HttpCacheUsage_VSInstanceCloseHandler;
        }

        internal class InstanceData
        {
            private int _hitCount = 0;
            internal int HitCount => _hitCount;

            private int _missCount = 0;
            internal int MissCount => _missCount;

            private int _cacheBypassCount = 0;
            internal int CacheBypassCount => _cacheBypassCount;

            private int _expiredCacheCount = 0;
            internal int ExpiredCacheCount => _expiredCacheCount;

            internal void IncrementCacheHitCount()
            {
                Interlocked.Increment(ref _hitCount);
            }

            internal void IncrementCacheMissCount(bool cacheByPass, bool isExpired)
            {
                Interlocked.Increment(ref _missCount);

                if (cacheByPass)
                {
                    Interlocked.Increment(ref _cacheBypassCount);
                }

                if (isExpired)
                {
                    Interlocked.Increment(ref _expiredCacheCount);
                }
            }

            internal void AddProperties(TelemetryEvent telemetryEvent)
            {
                telemetryEvent[HttpCache + NuGetHttpCacheUsageCollector.HitCount] = HitCount;
                telemetryEvent[HttpCache + NuGetHttpCacheUsageCollector.MissCount] = MissCount;
                telemetryEvent[HttpCache + NuGetHttpCacheUsageCollector.CacheBypassCount] = CacheBypassCount;
                telemetryEvent[HttpCache + NuGetHttpCacheUsageCollector.ExpiredCacheCount] = ExpiredCacheCount;
            }
        }
    }
}
