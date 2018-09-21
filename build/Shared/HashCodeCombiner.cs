// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Shared
{
    /// <summary>
    /// Hash code creator, based on the original NuGet hash code combiner/ASP hash code combiner implementations
    /// </summary>
    internal struct HashCodeCombiner
    {
        // seed from String.GetHashCode()
        private const long Seed = 0x1505L;

        private bool _initialized;
        private long _combinedHash;

        internal int CombinedHash
        {
            get { return _combinedHash.GetHashCode(); }
        }

        internal void AddInt32(int i)
        {
            CheckInitialized();
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
        }

        internal void AddObject(int i)
        {
            CheckInitialized();
            AddInt32(i);
        }

        internal void AddObject(bool b)
        {
            CheckInitialized();
            AddInt32(b.GetHashCode());
        }

        internal void AddObject<TValue>(TValue o, IEqualityComparer<TValue> comparer)
        {
            CheckInitialized();
            if (o != null)
            {
                AddInt32(comparer.GetHashCode(o));
            }
        }

        internal void AddObject(object o)
        {
            CheckInitialized();
            if (o != null)
            {
                AddInt32(o.GetHashCode());
            }
        }

        internal void AddStringIgnoreCase(string s)
        {
            CheckInitialized();
            if (s != null)
            {
                AddObject(s, StringComparer.OrdinalIgnoreCase);
            }
        }

        internal void AddSequence<T>(IEnumerable<T> sequence)
        {
            if (sequence != null)
            {
                foreach (var item in sequence)
                {
                    AddObject(item);
                }
            }
        }

        internal void AddDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> dictionary)
        {
            if (dictionary != null)
            {
                foreach (var pair in dictionary.OrderBy(x => x.Key))
                {
                    AddObject(pair.Key);
                    AddObject(pair.Value);
                }
            }
        }

        /// <summary>
        /// Create a unique hash code for the given set of items
        /// </summary>
        internal static int GetHashCode(params object[] objects)
        {
            var combiner = new HashCodeCombiner();

            foreach (var obj in objects)
            {
                combiner.AddObject(obj);
            }

            return combiner.CombinedHash;
        }

        private void CheckInitialized()
        {
            if (!_initialized)
            {
                _combinedHash = Seed;
                _initialized = true;
            }
        }
    }
}
