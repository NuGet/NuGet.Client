// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NuGet.Shared
{
    internal static class EqualityUtility
    {
        /// <summary>
        /// Compares two enumerables for equality using an optional comparer. Handles null values gracefully.
        /// </summary>
        /// <typeparam name="TSource">The type of the list</typeparam>
        /// <typeparam name="TKey">The type used for equality checking</typeparam>
        /// <param name="self">This list</param>
        /// <param name="other">The other list</param>
        /// <param name="keySelector">The function to extract the key from each item in the list</param>
        /// <param name="equalityComparer">An optional comparer for sequences</param>
        internal static bool ElementsEqual<TSource, TKey>(this IEnumerable<TSource>? self, IEnumerable<TSource>? other, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? equalityComparer = null) where TKey : notnull
        {
            Debug.Assert(equalityComparer != null || typeof(TKey) != typeof(string), "Argument " + nameof(equalityComparer) + " must be provided if " + nameof(TKey) + " is a string.");

            if (TryIdentityEquals(self, other, out bool identityEquals))
            {
                return identityEquals;
            }

            int selfCount = 0;
            equalityComparer ??= EqualityComparer<TKey>.Default;
            var sourceItems = new Dictionary<TKey, int>(equalityComparer);
            foreach (TSource current in self)
            {
                ++selfCount;
                TKey key = keySelector(current);
                if (sourceItems.TryGetValue(key, out int count))
                {
                    sourceItems[key] = count + 1;
                }
                else
                {
                    sourceItems[key] = 1;
                }
            }

            int otherCount = 0;
            foreach (TSource current in other)
            {
                ++otherCount;
                TKey key = keySelector(current);
                if (!sourceItems.TryGetValue(key, out int count) || count <= 0)
                {
                    return false;
                }
                else
                {
                    sourceItems[key] = count - 1;
                }
            }

            return selfCount == otherCount;
        }

        /// <summary>
        /// Compares two collections for equality using an optional comparer. Handles null values gracefully.
        /// </summary>
        /// <typeparam name="TSource">The type of the list</typeparam>
        /// <typeparam name="TKey">The type used for equality checking</typeparam>
        /// <param name="self">This list</param>
        /// <param name="other">The other list</param>
        /// <param name="keySelector">The function to extract the key from each item in the list</param>
        /// <param name="equalityComparer">An optional comparer for sequences</param>
        internal static bool ElementsEqual<TSource, TKey>(this ICollection<TSource>? self, ICollection<TSource>? other, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? equalityComparer = null) where TKey : notnull
        {
            Debug.Assert(equalityComparer != null || typeof(TKey) != typeof(string), "Argument " + nameof(equalityComparer) + " must be provided if " + nameof(TKey) + " is a string.");

            if (TryIdentityEquals(self, other, out bool identityEquals))
            {
                return identityEquals;
            }

            if (self.Count != other.Count)
            {
                return false;
            }

            if (self.Count == 0)
            {
                return true;
            }

            equalityComparer ??= EqualityComparer<TKey>.Default;
            if (self.Count == 1)
            {
                return equalityComparer.Equals(keySelector(self.First()), keySelector(other.First()));
            }

            Dictionary<TKey, int> sourceItems = new Dictionary<TKey, int>(self.Count, equalityComparer);
            foreach (TSource current in self)
            {
                TKey key = keySelector(current);
                if (sourceItems.TryGetValue(key, out int count))
                {
                    sourceItems[key] = count + 1;
                }
                else
                {
                    sourceItems[key] = 1;
                }
            }

            foreach (TSource current in other)
            {
                TKey key = keySelector(current);
                if (!sourceItems.TryGetValue(key, out int count) || count <= 0)
                {
                    return false;
                }
                else
                {
                    sourceItems[key] = count - 1;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two lists for equality using an optional comparer. Handles null values gracefully.
        /// </summary>
        /// <typeparam name="TSource">The type of the list</typeparam>
        /// <typeparam name="TKey">The type used for equality checking</typeparam>
        /// <param name="self">This list</param>
        /// <param name="other">The other list</param>
        /// <param name="equalityComparer">An optional comparer for sequences</param>
        internal static bool ElementsEqual<TSource, TKey>(this IList<TSource>? self, IList<TSource>? other, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? equalityComparer = null) where TKey : notnull
        {
            Debug.Assert(equalityComparer != null || typeof(TKey) != typeof(string), "Argument " + nameof(equalityComparer) + " must be provided if " + nameof(TKey) + " is a string.");

            if (TryIdentityEquals(self, other, out bool identityEquals))
            {
                return identityEquals;
            }

            if (self.Count != other.Count)
            {
                return false;
            }

            if (self.Count == 0)
            {
                return true;
            }

            equalityComparer ??= EqualityComparer<TKey>.Default;
            if (self.Count == 1)
            {
                return equalityComparer.Equals(keySelector(self[0]), keySelector(other[0]));
            }

            var sourceItems = new Dictionary<TKey, int>(self.Count, equalityComparer);
            for (int i = 0; i < self.Count; ++i)
            {
                TKey current = keySelector(self[i]);
                if (sourceItems.TryGetValue(current, out int count))
                {
                    sourceItems[current] = count + 1;
                }
                else
                {
                    sourceItems[current] = 1;
                }
            }

            for (int i = 0; i < other.Count; ++i)
            {
                TKey current = keySelector(other[i]);
                if (!sourceItems.TryGetValue(current, out int count) || count <= 0)
                {
                    return false;
                }
                else
                {
                    sourceItems[current] = count - 1;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two sequence for equality, allowing either sequence to be null. If one is null, both have to be
        /// null for equality.
        /// </summary>
        internal static bool SequenceEqualWithNullCheck<T>(
            this IEnumerable<T>? self,
            IEnumerable<T>? other,
            IEqualityComparer<T>? comparer = null)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            comparer ??= EqualityComparer<T>.Default;

            return self.SequenceEqual(other, comparer);
        }

        /// <summary>
        /// Compares two collections for equality, allowing either collection to be null. If one is null, both have to be
        /// null for equality.
        /// </summary>
        internal static bool SequenceEqualWithNullCheck<T>(
            this ICollection<T>? self,
            ICollection<T>? other,
            IEqualityComparer<T>? comparer = null)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            if (self.Count != other.Count)
            {
                return false;
            }

            if (self.Count == 0)
            {
                return true;
            }

            if (comparer == null)
            {
                comparer = EqualityComparer<T>.Default;
            }

            return self.SequenceEqual(other, comparer);
        }

        /// <summary>
        /// Compares two lists for equality, allowing either list to be null. If one is null, both have to be
        /// null for equality.
        /// </summary>
        internal static bool SequenceEqualWithNullCheck<T>(
            this IList<T>? self,
            IList<T>? other,
            IEqualityComparer<T>? comparer = null)
        {
            bool identityEquals;
            if (TryIdentityEquals(self, other, out identityEquals))
            {
                return identityEquals;
            }

            if (self.Count != other.Count)
            {
                return false;
            }

            if (self.Count == 0)
            {
                return true;
            }

            if (comparer == null)
            {
                comparer = EqualityComparer<T>.Default;
            }

            if (self.Count == 1)
            {
                return comparer.Equals(self[0], other[0]);
            }

            return self.SequenceEqual(other, comparer);
        }

        /// <summary>
        /// Compares two sets for equality, allowing either sequence to be null.
        /// If one is null, both have to be null for equality.
        /// </summary>
        internal static bool SetEqualsWithNullCheck<T>(
            this ISet<T>? self,
            ISet<T>? other,
            IEqualityComparer<T>? comparer = null)
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

            if (self.Count == 0)
            {
                return true;
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
            Func<TValue, TValue, bool>? compareValues = null)
            where TKey : notnull
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

            if (self.Count == 0)
            {
                return true;
            }

            if (!self.Keys.ElementsEqual(
                other.Keys,
                s => s,
                equalityComparer: EqualityComparer<TKey>.Default))
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
            where TKey : notnull
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

            return self?.Equals(other) ?? false;
        }

        /// <summary>
        /// Determines if the current string contains a value equal "false".  Leading and trailing whitespace are trimmed and the comparison is case-insensitive
        /// </summary>
        /// <param name="value">The string to compare.</param>
        /// <returns><see langword="true" /> if the current string is equal to a value of "false", otherwise <see langword="false" />.</returns>
        internal static bool EqualsFalse(this string value)
        {
            return !string.IsNullOrWhiteSpace(value) && bool.FalseString.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryIdentityEquals<T>(
            [NotNullWhen(returnValue: false)] T? self,
            [NotNullWhen(returnValue: false)] T? other,
            out bool equals)
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
