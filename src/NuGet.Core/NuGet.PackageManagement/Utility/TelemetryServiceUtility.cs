// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectManagement
{
    public static class TelemetryServiceUtility
    {
        public static void StartTimer(TelemetryServiceHelper telemetryHelper)
        {
            telemetryHelper?.StartorResumeTimer();
        }

        public static void EmitEvent(TelemetryServiceHelper telemetryHelper, string eventName)
        {
            if (telemetryHelper != null)
            {
                telemetryHelper.StopTimer();
                telemetryHelper.AddTelemetryEvent(eventName);
            }
        }

        public static void EmitEvent(TelemetryServiceHelper telemetryHelper, string eventName, double duration)
        {
            if (telemetryHelper != null)
            {
                telemetryHelper.AddTelemetryEvent(eventName, duration);
            }
        }

        public static void EmitEventAndRestartTimer(TelemetryServiceHelper telemetryHelper, string eventName)
        {
            if (telemetryHelper != null)
            {
                EmitEvent(telemetryHelper, eventName);
                telemetryHelper.StartorResumeTimer();
            }
        }
    }
}
