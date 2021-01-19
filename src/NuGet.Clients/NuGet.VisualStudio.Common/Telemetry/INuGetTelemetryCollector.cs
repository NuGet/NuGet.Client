// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.VisualStudio.Telemetry
{
    // Collect telemetry for aggregated telemetry emitting later at VS solution/instance close.
    public interface INuGetTelemetryCollector
    {
        void AddSolutionTelemetryEvent(Dictionary<string, object> telemetryData);
        IReadOnlyList<Dictionary<string, object>> GetVSSolutionTelemetryEvents();
        IReadOnlyList<Dictionary<string, object>> GetVSIntanceTelemetryEvents();
        void ClearSolutionTelemetryEvents();
    }
}
