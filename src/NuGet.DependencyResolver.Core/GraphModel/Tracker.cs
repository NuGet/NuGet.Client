using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.DependencyResolver
{
    public class Tracker<TItem>
    {
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

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
            return entry.List.All(known => item.Key.Version >= known.Key.Version);
        }

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