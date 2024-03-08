// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Replacement token table organized by property.
    /// </summary>
    public class PatternTable
    {
        private readonly Dictionary<string, Dictionary<ReadOnlyMemory<char>, object>> _table
            = new Dictionary<string, Dictionary<ReadOnlyMemory<char>, object>>(StringComparer.Ordinal);

        public PatternTable()
            : this(Enumerable.Empty<PatternTableEntry>())
        {
        }

        public PatternTable(IEnumerable<PatternTableEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            foreach (var entry in entries)
            {
                Dictionary<ReadOnlyMemory<char>, object> byProp;
                if (!_table.TryGetValue(entry.PropertyName, out byProp))
                {
                    byProp = new Dictionary<ReadOnlyMemory<char>, object>(ReadOnlyMemoryCharComparerOrdinal.Instance);
                    _table.Add(entry.PropertyName, byProp);
                }

                byProp.Add(entry.Name.AsMemory(), entry.Value);
            }
        }

        /// <summary>
        /// Lookup a token and get the replacement if it exists.
        /// </summary>
        /// <param name="propertyName">Property moniker</param>
        /// <param name="name">Token name</param>
        /// <param name="value">Replacement value</param>
        public bool TryLookup(string propertyName, ReadOnlyMemory<char> name, out object value)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            Dictionary<ReadOnlyMemory<char>, object> byProp;
            if (_table.TryGetValue(propertyName, out byProp))
            {
                return byProp.TryGetValue(name, out value);
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Represents a comparer of <see cref="ReadOnlyMemory{T}" /> that uses string ordinal comparison.
        /// </summary>
        private class ReadOnlyMemoryCharComparerOrdinal : IEqualityComparer<ReadOnlyMemory<char>>
        {
            public static ReadOnlyMemoryCharComparerOrdinal Instance { get; } = new ReadOnlyMemoryCharComparerOrdinal();

            private ReadOnlyMemoryCharComparerOrdinal()
            {
            }

            public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
            {
                return x.Span.Equals(y.Span, StringComparison.Ordinal);
            }

            public unsafe int GetHashCode(ReadOnlyMemory<char> obj)
            {
                if (obj.Length == 0)
                {
                    return 0;
                }

                fixed (char* pSpan0 = obj.Span)
                {
                    int num1 = 0x15051505;
                    int num2 = num1;

                    int* pSpan = (int*)pSpan0;

                    int charactersRemaining;

                    for (charactersRemaining = obj.Length; charactersRemaining >= 4; charactersRemaining -= 4)
                    {
                        num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ *pSpan;
                        num2 = ((num2 << 5) + num2 + (num2 >> 27)) ^ pSpan[1];
                        pSpan += 2;
                    }

                    if (charactersRemaining > 0)
                    {
                        num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ pSpan0[obj.Length - 1];
                    }

                    return (num1 + (num2 * 0x5D588B65)) & 0x7FFFFFFF;
                }
            }
        }
    }
}
