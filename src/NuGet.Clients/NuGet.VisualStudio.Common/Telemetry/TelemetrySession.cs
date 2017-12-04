// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.ProjectManagement;
using VsTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;
using VsTelemetryService = Microsoft.VisualStudio.Telemetry.TelemetryService;

namespace NuGet.VisualStudio.Telemetry
{
    public class TelemetrySession : ITelemetrySession
    {
        public static readonly TelemetrySession Instance = new TelemetrySession();

        private TelemetrySession() { }

        public void PostEvent(TelemetryEvent telemetryEvent)
        {
            VsTelemetryService.DefaultSession.PostEvent(ToVsTelemetryEvent(telemetryEvent));
        }

        public static VsTelemetryEvent ToVsTelemetryEvent(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            var vsTelemetryEvent = new VsTelemetryEvent(TelemetryConstants.VSEventNamePrefix + telemetryEvent.Name);

            foreach (var pair in telemetryEvent.Properties)
            {
                vsTelemetryEvent.Properties[TelemetryConstants.VSPropertyNamePrefix + pair.Key] = pair.Value;
            }

            return vsTelemetryEvent;
        }
    }
}
