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
        private IDictionary<string, object> _piiProperties;

        public TelemetryEvent(string eventName) :
            this(eventName, new Dictionary<string, object>())
        {
        }

        public TelemetryEvent(string eventName, Dictionary<string, object> properties)
        {
            Name = eventName;
            _properties = properties;
            _piiProperties = new Dictionary<string, object>();
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
                    _properties.TryGetValue(key, out var value);
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

        public void AddPiiData(string key, object value)
        {
            _piiProperties[key] = value;
        }

        public IEnumerable<KeyValuePair<string, object>> GetPiiData()
        {
            return _piiProperties;
        }
    }
}
