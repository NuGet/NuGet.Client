// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGet.Resolver
{
    /// <summary>
    /// Hash code creator, based on the original NuGet hash code combiner/ASP hash code combiner implementations
    /// </summary>
    internal sealed class HashCodeCombiner
    {
        // seed from String.GetHashCode()
        private const long Seed = 0x1505L;

        private long _combinedHash;

        internal HashCodeCombiner()
        {
            _combinedHash = Seed;
        }

        internal int CombinedHash
        {
            get { return _combinedHash.GetHashCode(); }
        }

        internal void AddInt32(int i)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
        }

        internal void AddObject(int i)
        {
            AddInt32(i);
        }

        internal void AddObject(bool b)
        {
            AddInt32(b.GetHashCode());
        }

        internal void AddObject(object o)
        {
            if (o != null)
            {
                AddInt32(o.GetHashCode());
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
    }
}
