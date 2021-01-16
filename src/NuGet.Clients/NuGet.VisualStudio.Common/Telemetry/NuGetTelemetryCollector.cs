// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(INuGetTelemetryCollector))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class NuGetTelemetryCollector : INuGetTelemetryCollector
    {
        // _solutionTelemetryEvents hold telemetry for current VS solution session.
        private readonly List<TelemetryEvent> _vsSolutionTelemetryEvents;
        // _vsInstanceTelemetryEvents hold telemetry for current VS instance session.
        private readonly List<TelemetryEvent> _vsInstanceTelemetryEvents;

        public NuGetTelemetryCollector()
        {
            _vsSolutionTelemetryEvents = new List<TelemetryEvent>();
            _vsInstanceTelemetryEvents = new List<TelemetryEvent>();
        }

        // Adds telemetry into list which will be aggregated by end of VS solution sessions or VS instance session.
        public void AddSolutionTelemetryEvent(TelemetryEvent telemetryData)
        {
            _vsSolutionTelemetryEvents.Add(telemetryData);
            _vsInstanceTelemetryEvents.Add(telemetryData);
        }

        public IReadOnlyList<TelemetryEvent> GetSolutionTelemetryEvents() => _vsSolutionTelemetryEvents.AsReadOnly();
        public IReadOnlyList<TelemetryEvent> GetVSIntanceTelemetryEvents() => _vsInstanceTelemetryEvents.AsReadOnly();
    }
}
