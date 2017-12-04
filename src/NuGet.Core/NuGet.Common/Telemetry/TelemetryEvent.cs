// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{
    /// <summary>
    /// This will be used to pass different nuget telemetry events data to vs telemetry service.
    /// </summary>
    public class TelemetryEvent
    {
        private IDictionary<string, object> _properties;

        public TelemetryEvent(string eventName, Dictionary<string, object> properties)
        {
            Name = eventName;
            _properties = properties;
        }

        public string Name { get; }

        /// <summary>
        /// Property count in TelemetryEvent
        /// </summary>
        public int Count => _properties.Count;

        public object this[string key]
        {
            get
            {
                if (key != null)
                {
                    _properties.TryGetValue(key, out object value);
                    return value;
                }
                return null;
            }
            set
            {
                _properties[key] = value;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _properties.GetEnumerator();
        }
    }
}
