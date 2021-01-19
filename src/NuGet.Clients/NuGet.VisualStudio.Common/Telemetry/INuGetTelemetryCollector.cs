// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    // Collect telemetry for aggregated telemetry emitting later at VS solution/instance close.
    public interface INuGetTelemetryCollector
    {
        void AddSolutionTelemetryEvent(TelemetryEvent telemetryData);
        IReadOnlyList<TelemetryEvent> GetVSSolutionTelemetryEvents();
        IReadOnlyList<TelemetryEvent> GetVSIntanceTelemetryEvents();
        void ClearSolutionTelemetryEvents();
    }
}
