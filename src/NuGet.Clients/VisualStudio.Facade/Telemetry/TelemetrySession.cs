// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if VS14
using System;
using Microsoft.VisualStudio.Telemetry;
using VsTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;
#endif

namespace NuGet.VisualStudio.Facade.Telemetry
{
#if VS14
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
#elif VS15
    public class TelemetrySession : ITelemetrySession
    {
        public void PostEvent(TelemetryEvent telemetryEvent)
        {
            // Do nothing.
        }
    }
#endif
}
