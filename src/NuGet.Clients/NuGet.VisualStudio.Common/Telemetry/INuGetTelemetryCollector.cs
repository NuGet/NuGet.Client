// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    // Emit aggregated telemetry at VS solution close or VS instance close.
    public interface INuGetTelemetryCollector
    {
        /// <summary> Add a <see cref="TelemetryEvent"/> to telemetry list which will be aggregated and emitted later. </summary>
        /// <param name="telemetryData"> Telemetry event to add into aggregation. </param>
        void AddSolutionTelemetryEvent(TelemetryEvent telemetryData);
    }
}
