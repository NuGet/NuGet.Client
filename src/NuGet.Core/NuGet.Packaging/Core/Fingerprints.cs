// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System;

namespace NuGet.Packaging.Core
{
    public class Fingerprints
    {
        private IDictionary<string, string> _keyValuePairs;

        public Fingerprints(IDictionary<string, string> fingerPrints)
        {
            _keyValuePairs = fingerPrints ?? throw new ArgumentNullException(nameof(fingerPrints));
        }

        // Get fingerprint from hash algorithm oid.
        public string this[string key]
        {
            get
            {
                if (key != null)
                {
                    _keyValuePairs.TryGetValue(key, out var value);

                    return value;
                }

                return null;
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _keyValuePairs.GetEnumerator();
        }
    }
}
