// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary> Abstraction of NuGet telemetry service. </summary>
    public interface INuGetTelemetryService
    {
        /// <summary> Send a <see cref="TelemetryEvent"/> to VS telemetry. </summary>
        /// <param name="telemetryData"> Telemetry event to send. </param>
        void EmitTelemetryEvent(TelemetryEvent telemetryData);

        /// <summary> Log a <see cref="TelemetryMarker"/> to the event log. </summary>
        /// <param name="telemetryMarkerName"> Name of telemetry marker to log. </param>
        void EmitTelemetryMarker(string telemetryMarkerName);
    }
}
