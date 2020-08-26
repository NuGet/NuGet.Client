// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Shared
{
    internal static class EqualityUtility
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

            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            return self
                .OrderBy(keySelector, orderComparer)
                .SequenceEqual(other.OrderBy(keySelector, orderComparer), sequenceComparer);
        }

        /// <summary>
        /// Compares two sequence for equality, allowing either sequence to be null. If one is null, both have to be
        /// null for equality.
        /// </summary>
        internal static bool SequenceEqualWithNullCheck<T>(
            this IEnumerable<T> self,
            IEnumerable<T> other,
            IEqualityComparer<T> comparer = null)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            if (comparer == null)
            {
                comparer = EqualityComparer<T>.Default;
            }

            return self.SequenceEqual(other, comparer);
        }

        /// <summary>
        /// Compares two sets for equality, allowing either sequence to be null.
        /// If one is null, both have to be null for equality.
        /// </summary>
        internal static bool SetEqualsWithNullCheck<T>(
            this ISet<T> self,
            ISet<T> other,
            IEqualityComparer<T> comparer = null)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            // Verify they could be equal by count
            if (self.Count != other.Count)
            {
                return false;
            }

            if (comparer == null)
            {
                comparer = EqualityComparer<T>.Default;
            }

            var set = new HashSet<T>(self, comparer);

            return set.SetEquals(other);
        }

        internal static bool DictionaryEquals<TKey, TValue>(
            IDictionary<TKey, TValue> self,
            IDictionary<TKey, TValue> other,
            Func<TValue, TValue, bool> compareValues = null)
        {
            var comparer = EqualityComparer<TValue>.Default;
            Func<TValue, TValue, bool> comparerFunc = (s, o) => comparer.Equals(s, o);
            compareValues = compareValues ?? comparerFunc;

            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            // Verify they could be equal by count
            if (self.Count != other.Count)
            {
                return false;
            }

            if (!self.Keys.OrderedEquals(
                other.Keys,
                s => s,
                orderComparer: Comparer<TKey>.Default,
                sequenceComparer: EqualityComparer<TKey>.Default))
            {
                return false;
            }

            foreach (var key in self.Keys)
            {
                if (!compareValues(self[key], other[key]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool DictionaryOfSequenceEquals<TKey, TValue>(
            IDictionary<TKey, IEnumerable<TValue>> self,
            IDictionary<TKey, IEnumerable<TValue>> other)
        {
            return DictionaryEquals(
                self,
                other,
                (selfValue, otherValue) => SequenceEqualWithNullCheck(selfValue, otherValue));
        }

        internal static bool EqualsWithNullCheck<T>(T self, T other)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            return self.Equals(other);
        }

        private static bool TryIdentityEquals<T>(T self, T other, out bool equals)
        {
            // Are they the same instance? This handles the case where both are null.
            if (ReferenceEquals(self, other))
            {
                equals = true;
                return true;
            }

            // Is only one of the sequences null?
            if (self == null || other == null)
            {
                equals = false;
                return true;
            }

            // Inconclusive.
            equals = false;
            return false;
        }
    }
}
