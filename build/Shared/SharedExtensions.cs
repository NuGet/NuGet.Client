// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Shared
{
    internal static class Extensions
    {
        /// <summary>
        /// Return the enumerable as a List of T, copying if required. Optimized for common case where it is an List of T.
        /// Avoid mutating the return value.
        /// </summary>
        /// <remarks>https://aspnetwebstack.codeplex.com/SourceControl/latest#src/Common/CollectionExtensions.cs</remarks>
        public static List<T> AsList<T>(this IEnumerable<T> enumerable)
        {
            var list = enumerable as List<T>;
            if (list != null)
            {
                return list;
            }

            return new List<T>(enumerable);
        }

        /// <summary>
        /// Return the ISet as a HashSet of T, copying if required. Optimized for common case where it is a HashSet of T.
        /// Avoid mutating the return value.
        /// </summary>
        public static HashSet<T>? AsHashSet<T>(this ISet<T> enumerable, IEqualityComparer<T>? comparer = null)
        {
            if (enumerable == null)
            {
                return null;
            }

            var set = enumerable as HashSet<T>;
            if (set != null)
            {
                return set;
            }
            else
            {
                if (comparer == null)
                {
                    comparer = EqualityComparer<T>.Default;
                }

                return new HashSet<T>(enumerable, comparer);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (var item in enumeration)
            {
                action(item);
            }
        }


        /// <summary>
        /// Helper function to append an <see cref="int"/> to a <see cref="StringBuilder"/>. Calling
        /// <see cref="StringBuilder.Append(int)"/> directly causes an allocation by first converting the
        /// <see cref="int"/> to a string and then appending that result:
        /// <code>
        /// public StringBuilder Append(int value)
        /// {
        ///     return Append(value.ToString(CultureInfo.CurrentCulture));
        /// }
        /// </code>
        ///
        /// Note that this uses the current culture to do the conversion while <see cref="AppendInt(StringBuilder, int)"/> does
        /// not do any cultural sensitive conversion.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
        /// <param name="value">The <see cref="int"/> to append.</param>
        public static void AppendInt(this StringBuilder sb, int value)
        {
            if (value == 0)
            {
                sb.Append('0');
                return;
            }

            // special case min value since it'll overflow if we negate it
            if (value == int.MinValue)
            {
                sb.Append("-2147483648");
                return;
            }

            // do all math with positive integers
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }

            // upper range of int is 1 billion, so we start dividing by that to get the digit at that position
            int divisor = 1_000_000_000;

            // remember when we found our first digit so we can keep adding intermediate zeroes
            bool digitFound = false;
            while (divisor > 0)
            {
                if (digitFound || value >= divisor)
                {
                    digitFound = true;
                    int digit = value / divisor;
                    value -= digit * divisor;

                    // convert the digit to char by adding the value to '0'.
                    // '0' + 0 = 48 + 0 = 48 = '0'
                    // '0' + 1 = 48 + 1 = 49 = '1'
                    // '0' + 2 = 48 + 2 = 50 = '2'
                    // etc...
                    sb.Append((char)('0' + digit));
                }

                divisor /= 10;
            }
        }
    }
}
