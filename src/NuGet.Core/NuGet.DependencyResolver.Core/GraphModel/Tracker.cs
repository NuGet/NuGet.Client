// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class Tracker<TItem>
    {
        private readonly Dictionary<string, Entry> _entries
            = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public void Track(GraphItem<TItem> item)
        {
            var entry = GetEntry(item);
            if (!entry.List.Contains(item))
            {
                entry.List.Add(item);
            }
        }

        public bool IsDisputed(GraphItem<TItem> item)
        {
            return GetEntry(item).List.Count > 1;
        }

        public bool IsAmbiguous(GraphItem<TItem> item)
        {
            return GetEntry(item).Ambiguous;
        }

        public void MarkAmbiguous(GraphItem<TItem> item)
        {
            GetEntry(item).Ambiguous = true;
        }

        public bool IsBestVersion(GraphItem<TItem> item)
        {
            var entry = GetEntry(item);

            // If there are any references then that's the only list we need to consider since
            // it always wins over non-references
            var candidates = entry.List.Where(known => known.Key.Type == LibraryTypes.Reference);

            if (!candidates.Any())
            {
                // No references, just use the entire set
                candidates = entry.List;
            }

            // Normal version check
            return candidates.Contains(item) && candidates.All(known => item.Key.Version >= known.Key.Version);
        }

        public IEnumerable<GraphItem<TItem>> GetDisputes(GraphItem<TItem> item) => GetEntry(item).List;

        private Entry GetEntry(GraphItem<TItem> item)
        {
            Entry itemList;
            if (!_entries.TryGetValue(item.Key.Name, out itemList))
            {
                itemList = new Entry();
                _entries[item.Key.Name] = itemList;
            }
            return itemList;
        }

        private class Entry
        {
            public Entry()
            {
                List = new HashSet<GraphItem<TItem>>();
            }

            public HashSet<GraphItem<TItem>> List { get; set; }

            public bool Ambiguous { get; set; }
        }
    }
}
