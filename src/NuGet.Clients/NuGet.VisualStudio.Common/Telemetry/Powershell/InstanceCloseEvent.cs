// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio.Common.Telemetry.PowerShell
{
    public static class InstanceCloseEvent
    {
        private const string EventName = "InstanceClose";

        public static void OnShutdown()
        {
            var telemetryEvent = new TelemetryEvent(EventName);

            AddEventsOnShutdown?.Invoke(null, telemetryEvent);

            telemetryEvent["faults.total"] = TelemetryUtility.TotalFaultEvents;

            TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
        }

        public static event System.EventHandler<TelemetryEvent> AddEventsOnShutdown;
    }
}
