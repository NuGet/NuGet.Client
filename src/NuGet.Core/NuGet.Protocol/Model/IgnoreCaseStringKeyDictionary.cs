// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Protocol.Converters;

namespace NuGet.Protocol.Model
{
    [JsonConverter(typeof(VulnerabilityFileDataConverter))]
    internal class VulnerabilityFileData : IReadOnlyDictionary<string, IReadOnlyList<VulnerabilityInfo>>
    {
        private IReadOnlyDictionary<string, IReadOnlyList<VulnerabilityInfo>> _inner;

        public VulnerabilityFileData(Dictionary<string, IReadOnlyList<VulnerabilityInfo>> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(paramName: nameof(inner));

            if (inner.Comparer != StringComparer.InvariantCultureIgnoreCase)
            {
                throw new ArgumentException(
                    message: "Dictionary does not use " + nameof(StringComparer.InvariantCultureIgnoreCase) + " as the comparer",
                    paramName: nameof(inner));
            }
        }

        public IReadOnlyList<VulnerabilityInfo> this[string key] => _inner[key];

        public IEnumerable<string> Keys => _inner.Keys;

        public IEnumerable<IReadOnlyList<VulnerabilityInfo>> Values => _inner.Values;

        public int Count => _inner.Count;

        public bool ContainsKey(string key) => _inner.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, IReadOnlyList<VulnerabilityInfo>>> GetEnumerator() => _inner.GetEnumerator();

        public bool TryGetValue(string key, out IReadOnlyList<VulnerabilityInfo> value) => _inner.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}
