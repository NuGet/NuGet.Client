﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Shared
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

        internal HashCodeCombiner AddInt32(int i)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
            return this;
        }

        internal HashCodeCombiner AddObject(int i)
        {
            AddInt32(i);
            return this;
        }

        internal HashCodeCombiner AddObject(bool b)
        {
            AddInt32(b.GetHashCode());
            return this;
        }

        internal HashCodeCombiner AddObject(object o)
        {
            if (o != null)
            {
                AddInt32(o.GetHashCode());
            }
            return this;
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
