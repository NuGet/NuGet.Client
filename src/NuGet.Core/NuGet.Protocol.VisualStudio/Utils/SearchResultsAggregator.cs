using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol.VisualStudio.Services
{
    public class SearchResultsAggregator : ISearchResultsAggregator
    {
        private readonly ISearchResultsIndexer _indexer;

        public SearchResultsAggregator(ISearchResultsIndexer indexer)
        {
            if (indexer == null)
            {
                throw new ArgumentNullException(nameof(indexer));
            }

            _indexer = indexer;
        }

        public IEnumerable<PackageSearchMetadata> Aggregate(string queryString, params IEnumerable<PackageSearchMetadata>[] inputResults)
        {
            var mergedIndex = new MergedIndex();
            foreach(var inputResult in inputResults)
            {
                mergedIndex.MergeResult(inputResult);
            }

            var ranking = _indexer.Rank(queryString, mergedIndex.Entries);

            var inputQueues = inputResults
                .Select(result => new Queue<string>(result.Select(entry => entry.Identity.Id)))
                .ToArray();

            var enqueued = new HashSet<string>();
            var outputQueue = new Queue<PackageSearchMetadata>(mergedIndex.Entries.Count());

            while (inputQueues.Any(q => !q.IsEmpty()))
            {
                foreach(var queue in inputQueues)
                {
                    // remove elements with no rank and already enqueued ones
                    while (!queue.IsEmpty() && (enqueued.Contains(queue.Peek()) || !ranking.ContainsKey(queue.Peek())))
                    {
                        queue.Dequeue();
                    }
                }

                var candidates = inputQueues.Where(q => !q.IsEmpty()).Select(q => q.Peek()).ToArray();
                if (!candidates.IsEmpty())
                {
                    var winner = candidates.Aggregate((w, x) => (w == null || ranking[x] > ranking[w]) ? x : w);
                    enqueued.Add(winner);
                    outputQueue.Enqueue(mergedIndex[winner]);
                }
            }

            return outputQueue;
        }

        private class MergedIndex
        {
            private readonly IDictionary<string, PackageSearchMetadata> _index = new Dictionary<string, PackageSearchMetadata>(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<PackageSearchMetadata> Entries => _index.Values;

            public PackageSearchMetadata this[string key] => _index[key];

            public void MergeResult(IEnumerable<PackageSearchMetadata> result)
            {
                foreach (var entry in result)
                {
                    PackageSearchMetadata value;
                    if (_index.TryGetValue(entry.Identity.Id, out value))
                    {
                        _index[entry.Identity.Id] = MergeEntries(value, entry);
                    }
                    else
                    {
                        _index.Add(entry.Identity.Id, entry);
                    }
                }
            }

            private static PackageSearchMetadata MergeEntries(PackageSearchMetadata lhs, PackageSearchMetadata rhs)
            {
                var mergedVersions = lhs.Versions.Concat(rhs.Versions).Distinct().ToArray();
                var newerEntry = (lhs.Identity.Version >= rhs.Identity.Version) ? lhs : rhs;
                newerEntry.Versions = mergedVersions;
                return newerEntry;
            }
        }
    }
}
