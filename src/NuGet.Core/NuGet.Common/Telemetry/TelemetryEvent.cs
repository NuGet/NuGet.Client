// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private IDictionary<string, IEnumerable<string>> _piiLists;
        private IDictionary<string, Tuple<string, string>> _piiPackages;
        private IDictionary<string, IEnumerable<Tuple<string, string>>> _piiPackageLists;

        public TelemetryEvent(string eventName) :
            this(eventName, new Dictionary<string, object>())
        {
        }

        public TelemetryEvent(string eventName, Dictionary<string, object> properties)
        {
            Name = eventName;
            _properties = properties;
            _piiProperties = new Dictionary<string, object>();
            _piiLists = new Dictionary<string, IEnumerable<string>>();
            _piiPackages = new Dictionary<string, Tuple<string, string>>();
            _piiPackageLists = new Dictionary<string, IEnumerable<Tuple<string, string>>>();
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

        public IDictionary<string, object> ComplexData { get; } = new Dictionary<string, object>();

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

        public IEnumerable<KeyValuePair<string, Tuple<string, string>>> GetPiiPackages()
        {
            return _piiPackages;
        }

        public void AddListOfPiiValues(string key, IEnumerable<string> listOfValues)
        {
            _piiLists.Add(key, listOfValues);
        }

        public void AddPiiPackage(string key, Tuple<string, string> packageInfo)
        {
            _piiPackages.Add(key, packageInfo);
        }

        public void AddPiiPackageList(string key, IEnumerable<Tuple<string, string>> listOfTuples)
        {
            _piiPackageLists.Add(key, listOfTuples);
        }

        public IEnumerable<KeyValuePair<string,IEnumerable<Tuple<string, string>>>> GetPiiPackageList()
        {
            return _piiPackageLists;
        }


        public IEnumerable<KeyValuePair<string,IEnumerable<string>>> GetPiiLists() {
            return _piiLists;
        }

    }
}
