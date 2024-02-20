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
        private readonly Dictionary<string, object> _table
            = new Dictionary<string, object>(StringComparer.Ordinal);

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
                _table.Add(entry.PropertyName, entry.Value);
            }
        }

        /// <summary>
        /// Lookup a token and get the replacement if it exists.
        /// </summary>
        /// <param name="propertyName">Property moniker</param>
        /// <param name="name">Token name</param>
        /// <param name="value">Replacement value</param>
        public bool TryLookup(string propertyName, ReadOnlySpan<char> name, out object value)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (propertyName.Equals("tfm", StringComparison.CurrentCultureIgnoreCase))
            {
                return _table.TryGetValue(name, out value);
            }

            value = null;
            return false;
        }
    }
}
