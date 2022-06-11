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

        private void HttpSourceUsage_HttpSourceMissCacheEventHandler()
        {
            _vsInstanceData.IncrementCacheMissCount();
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
            internal int MissCount { get; set; }

            internal InstanceData()
            {
                _hitCount = 0;
                _missCount = 0;
            }

            internal void IncrementCacheHitCount()
            {
                Interlocked.Increment(ref _hitCount);
            }

            internal void IncrementCacheMissCount()
            {
                Interlocked.Increment(ref _missCount);
            }

            internal void AddProperties(TelemetryEvent telemetryEvent)
            {
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.HitCount] = HitCount;
                telemetryEvent[HttpSource + NuGetHttpSourceTelemetryCollector.MissCount] = MissCount;
            }
        }
    }
}
