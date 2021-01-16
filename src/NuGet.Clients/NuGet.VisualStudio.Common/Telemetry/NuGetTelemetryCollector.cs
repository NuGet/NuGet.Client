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
        private ConcurrentBag<TelemetryEvent> _vsSolutionTelemetryEvents;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private readonly ConcurrentBag<TelemetryEvent> _vsInstanceTelemetryEvents;

        public NuGetTelemetryCollector()
        {
            _vsSolutionTelemetryEvents = new ConcurrentBag<TelemetryEvent>();
            _vsInstanceTelemetryEvents = new ConcurrentBag<TelemetryEvent>();
        }

        // Adds telemetry into list which will be aggregated by end of VS solution sessions or VS instance session.
        public void AddSolutionTelemetryEvent(TelemetryEvent telemetryData)
        {
            _vsSolutionTelemetryEvents.Add(telemetryData);
            _vsInstanceTelemetryEvents.Add(telemetryData);
        }

        public IReadOnlyList<TelemetryEvent> GetSolutionTelemetryEvents() => _vsSolutionTelemetryEvents.ToList().AsReadOnly();
        public IReadOnlyList<TelemetryEvent> GetVSIntanceTelemetryEvents() => _vsInstanceTelemetryEvents.ToList().AsReadOnly();

        // If open new solution then need ability to reset existing solution events.
        public void ClearSolutionTelemetryEvents()
        {
            var newBag = new ConcurrentBag<TelemetryEvent>();
            Interlocked.Exchange<ConcurrentBag<TelemetryEvent>>(ref _vsSolutionTelemetryEvents, newBag);
        }
    }
}
