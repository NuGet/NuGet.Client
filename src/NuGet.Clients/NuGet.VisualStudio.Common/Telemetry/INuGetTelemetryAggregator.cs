// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    // This one used for emitting aggregated telemetry at VS solution close or VS instance close.
    public interface INuGetTelemetryAggregator
    {
        /// <summary> Add a <see cref="TelemetryEvent"/> to telemetry list which will be aggregated and sent later. </summary>
        /// <param name="telemetryData"> Telemetry event to send. </param>
        void AddSolutionTelemetryEvent(TelemetryEvent telemetryData);
    }
}
