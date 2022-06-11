// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.VisualStudio.Common.Telemetry.PowerShell;

namespace NuGet.VisualStudio.Common.Telemetry
{
    public sealed class NuGetHttpSourceTelemetryCollector : IDisposable
    {
        // PMC, PMUI powershell telemetry consts
        public const string HitCount = nameof(HitCount);
        public const string MissCount = nameof(MissCount);
        public const string CacheByPassCount = nameof(CacheByPassCount);
        public const string ExpiredCacheCount = nameof(ExpiredCacheCount);
        public const string HttpSource = "HttpSource.";

        private readonly InstanceData _vsInstanceData;
        private object _lock = new object();

        public NuGetHttpSourceTelemetryCollector()
        {
            _vsInstanceData = new InstanceData();
            HttpSourceUsage.HttpSourceHitCacheEvent += HttpSourceUsage_HttpSourceHitCacheEventHandler;
            HttpSourceUsage.HttpSourceMissCacheEvent += HttpSourceUsage_HttpSourceMissCacheEventHandler;

            InstanceCloseEvent.AddEventsOnShutdown += NuGetSourceTelemetry_VSInstanseCloseHandler;
        }

        private void HttpSourceUsage_HttpSourceHitCacheEventHandler()
        {
            _vsInstanceData.IncrementCacheHitCount();
        }

        private void HttpSourceUsage_HttpSourceMissCacheEventHandler(bool cacheByPass, bool isExpired)
        {
            _vsInstanceData.IncrementCacheMissCount(cacheByPass, isExpired);
        }

        private void NuGetSourceTelemetry_VSInstanseCloseHandler(object sender, TelemetryEvent telemetryEvent)
        {
            lock (_lock)
            {
                // Add VS Instance telemetry
                _vsInstanceData.AddProperties(telemetryEvent);
            }
        }

        public void Dispose()
        {
            HttpSourceUsage.HttpSourceHitCacheEvent -= HttpSourceUsage_HttpSourceHitCacheEventHandler;
            HttpSourceUsage.HttpSourceMissCacheEvent -= HttpSourceUsage_HttpSourceMissCacheEventHandler;

            InstanceCloseEvent.AddEventsOnShutdown -= NuGetSourceTelemetry_VSInstanseCloseHandler;
        }

        internal class InstanceData
        {
            private int _hitCount = 0;
            internal int HitCount => _hitCount;

            private int _missCount = 0;
            internal int MissCount => _missCount;

            private int _cacheByPassCount = 0;
            internal int CacheByPassCount => _cacheByPassCount;

            private int _expiredCacheCount = 0;
            internal int ExpiredCacheCount => _expiredCacheCount;

            internal InstanceData()
            {
                _hitCount = 0;
                _missCount = 0;
                _cacheByPassCount = 0;
            }

            internal void IncrementCacheHitCount()
            {
                Interlocked.Increment(ref _hitCount);
            }

            internal void IncrementCacheMissCount(bool cacheByPass, bool isExpired)
            {
                Interlocked.Increment(ref _missCount);

                if (cacheByPass)
                {
                    Interlocked.Increment(ref _cacheByPassCount);
                }

                if (isExpired)
                {
                    Interlocked.Increment(ref _expiredCacheCount);
                }
            }

            internal void AddProperties(TelemetryEvent telemetryEvent)
            {
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.HitCount] = HitCount;
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.MissCount] = MissCount;
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.CacheByPassCount] = CacheByPassCount;
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.ExpiredCacheCount] = ExpiredCacheCount;
            }
        }
    }
}
