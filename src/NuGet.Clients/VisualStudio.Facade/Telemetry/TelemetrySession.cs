// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Telemetry;
using VsTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;

namespace NuGet.VisualStudio.Facade.Telemetry
{
    public class TelemetrySession : ITelemetrySession
    {
        public void PostEvent(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            var vsTelemetryEvent = new VsTelemetryEvent(telemetryEvent.Name);

            foreach (var pair in telemetryEvent.Properties)
            {
                vsTelemetryEvent.Properties[pair.Key] = pair.Value;
            }

            TelemetryService.DefaultSession.PostEvent(vsTelemetryEvent);
        }
    }
}
