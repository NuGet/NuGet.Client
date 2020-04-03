// All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Telemetry;
using TelemetryEvent = NuGet.Common.TelemetryEvent;

namespace NuGet.PackageManagement.Telemetry
{
    public class PerformanceMetrics
    {
        public TimeSpan? TimeSinceSearchCompleted { get; set; } = null;
        public TimeSpan? TimeSinceContainersGenerated { get; set; } = null;
        public TimeSpan? TimeSinceLayoutUpdated { get; set; } = null;
        public TimeSpan? TimeSinceOnRender { get; set; } = null;
        public TimeSpan? TimeSinceDispatcher { get; set; } = null;

        public void WriteTelemetry(TelemetryEvent telemetry)
        {
            telemetry["TimeSinceSearchCompleted"] = TimeSinceSearchCompleted.HasValue ? FormatTime(TimeSinceSearchCompleted.Value) : null;
            telemetry["TimeSinceContainersGenerated"] = TimeSinceContainersGenerated.HasValue ? FormatTime(TimeSinceContainersGenerated.Value) : null;
            telemetry["TimeSinceLayoutUpdated"] = TimeSinceLayoutUpdated.HasValue ? FormatTime(TimeSinceLayoutUpdated.Value) : null;
            telemetry["TimeSinceDispatcher"] = TimeSinceDispatcher.HasValue ? FormatTime(TimeSinceDispatcher.Value) : null;
            telemetry["TimeSinceOnRender"] = TimeSinceOnRender.HasValue ? FormatTime(TimeSinceOnRender.Value) : null;
        }

        private string FormatTime(TimeSpan time)
        {
            return time.ToString("ss\\.ffff");
        }
    }
}
