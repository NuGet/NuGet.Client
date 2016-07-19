// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.VisualStudio.Facade.Telemetry
{
    public class TelemetryEvent
    {
        public TelemetryEvent(string eventName)
        {
            Name = eventName;
            Properties = new Dictionary<string, object>();
        }

        public string Name { get; }
        public IDictionary<string, object> Properties { get; }
    }
}
