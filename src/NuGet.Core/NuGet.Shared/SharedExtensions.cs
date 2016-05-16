// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Shared
{
    internal static class Extensions
    {
        /// <summary>
        /// Compares two enumberables for equality, ordered according to the specified key and optional comparer. Handles null values gracefully.
        /// </summary>
        /// <typeparam name="TSource">The type of the list</typeparam>
        /// <typeparam name="TKey">The type of the sorting key</typeparam>
        /// <param name="self">This list</param>
        /// <param name="other">The other list</param>
        /// <param name="keySelector">The function to extract the key from each item in the list</param>
        /// <param name="orderComparer">An optional comparer for comparing keys</param>
        /// <param name="sequenceComparer">An optional comparer for sequences</param>
        internal static bool OrderedEquals<TSource, TKey>(this IEnumerable<TSource> self, IEnumerable<TSource> other, Func<TSource, TKey> keySelector, IComparer<TKey> orderComparer = null, IEqualityComparer<TSource> sequenceComparer = null)
        {
            Debug.Assert(orderComparer != null || typeof(TKey) != typeof(string), "Argument " + "orderComparer" + " must be provided if " + "TKey" + " is a string.");
            Debug.Assert(sequenceComparer != null || typeof(TSource) != typeof(string), "Argument " + "sequenceComparer" + " must be provided if " + "TSource" + " is a string.");

            if (ReferenceEquals(self, other))
            {
                return true;
            }

            if (self == null || other == null)
            {
                return false;
            }

            return self.OrderBy(keySelector, orderComparer).SequenceEqual(other.OrderBy(keySelector, orderComparer), sequenceComparer);
        }
    }
}
