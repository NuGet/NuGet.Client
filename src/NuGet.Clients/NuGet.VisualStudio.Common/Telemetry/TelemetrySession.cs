// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using VsTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;
using VsTelemetryService = Microsoft.VisualStudio.Telemetry.TelemetryService;
using VsTelemetryPiiProperty = Microsoft.VisualStudio.Telemetry.TelemetryPiiProperty;

namespace NuGet.VisualStudio.Telemetry
{
    public class VSTelemetrySession : ITelemetrySession
    {
        public static readonly VSTelemetrySession Instance = new VSTelemetrySession();

        public const string VSEventNamePrefix = "VS/NuGet/";
        public const string VSPropertyNamePrefix = "VS.NuGet.";

        private VSTelemetrySession() { }

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

            var vsTelemetryEvent = new VsTelemetryEvent(VSEventNamePrefix + telemetryEvent.Name);

            foreach (var pair in telemetryEvent)
            {
                vsTelemetryEvent.Properties[VSPropertyNamePrefix + pair.Key] = pair.Value;
            }

            foreach (var pair in telemetryEvent.GetPiiData())
            {
                vsTelemetryEvent.Properties[VSPropertyNamePrefix + pair.Key] = new VsTelemetryPiiProperty(pair.Value);
            }

            return vsTelemetryEvent;
        }
    }
}
