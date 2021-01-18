// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(INuGetTelemetryCollector))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class NuGetTelemetryCollector : INuGetTelemetryCollector
    {
        // _solutionTelemetryEvents hold telemetry for current VS solution session.
        private Lazy<ConcurrentBag<TelemetryEvent>> _vsSolutionTelemetryEvents;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private readonly Lazy<ConcurrentBag<TelemetryEvent>> _vsInstanceTelemetryEvents;

        public NuGetTelemetryCollector()
        {
            _vsSolutionTelemetryEvents = new Lazy<ConcurrentBag<TelemetryEvent>>();
            _vsInstanceTelemetryEvents = new Lazy<ConcurrentBag<TelemetryEvent>>();
        }

        /// <summary> Add a <see cref="TelemetryEvent"/> to telemetry list which will be aggregated and emitted later. </summary>
        /// <param name="telemetryData"> Telemetry event to add into aggregation. </param>
        public void AddSolutionTelemetryEvent(TelemetryEvent telemetryData)
        {
            _vsSolutionTelemetryEvents.Value.Add(telemetryData);
            _vsInstanceTelemetryEvents.Value.Add(telemetryData);
        }

        public IReadOnlyList<TelemetryEvent> GetVSSolutionTelemetryEvents() => _vsSolutionTelemetryEvents.Value.ToList();
        public IReadOnlyList<TelemetryEvent> GetVSIntanceTelemetryEvents() => _vsInstanceTelemetryEvents.Value.ToList();

        // If open new solution then need to clear previous solution events.
        public void ClearSolutionTelemetryEvents()
        {
            var newBag = new Lazy<ConcurrentBag<TelemetryEvent>>();
            Interlocked.Exchange(ref _vsSolutionTelemetryEvents, newBag);
        }
    }
}
