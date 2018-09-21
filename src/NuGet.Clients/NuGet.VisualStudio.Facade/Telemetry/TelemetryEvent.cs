// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.VisualStudio.Facade.Telemetry
{
    /// <summary>
    /// This will be used to pass different nuget telemetry events data to vs telemetry service.
    /// </summary>
    public class TelemetryEvent
    {
        public TelemetryEvent(string eventName, Dictionary<string, object> properties)
        {
            Name = eventName;
            Properties = properties;
        }

        public string Name { get; }

        public IDictionary<string, object> Properties { get; }
    }
}
