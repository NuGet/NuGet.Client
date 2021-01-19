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
        // _solutionTelemetryEvents hold telemetry data for current VS solution session.
        private Lazy<ConcurrentBag<Dictionary<string, object>>> _vsSolutionTelemetryEvents;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private readonly Lazy<ConcurrentBag<Dictionary<string, object>>> _vsInstanceTelemetryEvents;

        public NuGetTelemetryCollector()
        {
            _vsSolutionTelemetryEvents = new Lazy<ConcurrentBag<Dictionary<string, object>>>();
            _vsInstanceTelemetryEvents = new Lazy<ConcurrentBag<Dictionary<string, object>>>();
        }

        /// <summary> Add a <see cref="TelemetryEvent"/> to telemetry list which will be aggregated and emitted later. </summary>
        /// <param name="telemetryData"> Telemetry event to add into aggregation. </param>
        public void AddSolutionTelemetryEvent(Dictionary<string, object> telemetryData)
        {
            _vsSolutionTelemetryEvents.Value.Add(telemetryData);
            _vsInstanceTelemetryEvents.Value.Add(telemetryData);
        }

        public IReadOnlyList<Dictionary<string, object>> GetVSSolutionTelemetryEvents() => _vsSolutionTelemetryEvents.Value.ToList();
        public IReadOnlyList<Dictionary<string, object>> GetVSIntanceTelemetryEvents() => _vsInstanceTelemetryEvents.Value.ToList();

        // If open new solution then need to clear previous solution events.
        public void ClearSolutionTelemetryEvents()
        {
            var newBag = new Lazy<ConcurrentBag<Dictionary<string, object>>>();
            Interlocked.Exchange(ref _vsSolutionTelemetryEvents, newBag);
        }
    }
}
