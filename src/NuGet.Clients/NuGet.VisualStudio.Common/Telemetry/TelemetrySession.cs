// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using NuGet.Common;
using VsTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;
using VsTelemetryService = Microsoft.VisualStudio.Telemetry.TelemetryService;
using VsTelemetryPiiProperty = Microsoft.VisualStudio.Telemetry.TelemetryPiiProperty;
using VsTelemetryComplexProperty = Microsoft.VisualStudio.Telemetry.TelemetryComplexProperty;

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

            foreach (var pair in telemetryEvent.GetComplexData())
            {
                vsTelemetryEvent.Properties[VSPropertyNamePrefix + pair.Key] = ToComplexProperty(pair.Value);
            }

            return vsTelemetryEvent;
        }

        private static object ToComplexProperty(object value)
        {
            if (value is TelemetryEvent @event)
            {
                var dictionary = new Dictionary<string, object>();

                foreach (var pair in @event)

                {
                    dictionary[pair.Key] = pair.Value;
                }

                foreach (var pair in @event.GetPiiData())
                {
                    dictionary[pair.Key] = new VsTelemetryPiiProperty(pair.Value);
                }

                foreach (var pair in @event.GetComplexData())
                {
                    dictionary[pair.Key] = ToComplexProperty(pair.Value);
                }

                return new VsTelemetryComplexProperty(dictionary);
            }
            else if (value is IEnumerable enumerable)
            {
                var list = new List<object>();

                foreach (var item in list)
                {
                    list.Add(ToComplexProperty(item));
                }

                return list;
            }
            else
            {
                return value;
            }
        }
    }
}
