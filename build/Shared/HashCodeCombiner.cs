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

        private void AddInt32(int i)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
        }

        internal void AddObject(int i)
        {
            CheckInitialized();
            AddInt32(i);
        }

        internal void AddObject<TValue>(TValue o, IEqualityComparer<TValue> comparer)
        {
            CheckInitialized();
            if (o != null)
            {
                AddInt32(comparer.GetHashCode(o));
            }
        }

        internal void AddObject<T>(T o)
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

        internal void AddSequence<T>(T[] array)
        {
            if (array != null)
            {
                foreach (var item in array)
                {
                    AddObject(item);
                }
            }
        }

        internal void AddSequence<T>(IList<T> list)
        {
            if (list != null)
            {
                var count = list.Count;
                for (var i = 0; i < count; i++)
                {
                    AddObject(list[i]);
                }
            }
        }

#if !NET40
        internal void AddSequence<T>(IReadOnlyList<T> list)
        {
            if (list != null)
            {
                var count = list.Count;
                for (var i = 0; i < count; i++)
                {
                    AddObject(list[i]);
                }
            }
        }
#endif
        internal void AddDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> dictionary)
        {
            if (dictionary != null)
            {
                CheckInitialized();
                foreach (var pair in dictionary.OrderBy(x => x.Key))
                {
                    AddInt32(pair.Key.GetHashCode());
                    AddInt32(pair.Value.GetHashCode());
                }
            }
        }

        /// <summary>
        /// Create a unique hash code for the given set of items
        /// </summary>
        internal static int GetHashCode<T1, T2>(T1 o1, T2 o2)
        {
            var combiner = new HashCodeCombiner();
            combiner.CheckInitialized();

            combiner.AddInt32(o1.GetHashCode());
            combiner.AddInt32(o2.GetHashCode());

            return combiner.CombinedHash;
        }

        /// <summary>
        /// Create a unique hash code for the given set of items
        /// </summary>
        internal static int GetHashCode<T1, T2, T3>(T1 o1, T2 o2, T3 o3)
        {
            var combiner = new HashCodeCombiner();
            combiner.CheckInitialized();

            combiner.AddInt32(o1.GetHashCode());
            combiner.AddInt32(o2.GetHashCode());
            combiner.AddInt32(o3.GetHashCode());

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
