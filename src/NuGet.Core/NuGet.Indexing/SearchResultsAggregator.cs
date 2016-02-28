using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    /// <summary>
    /// Aggregator preserving relative order of search results in original collection.
    /// </summary>
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

        public async Task<IEnumerable<IPackageSearchMetadata>> AggregateAsync(string queryString, params IEnumerable<IPackageSearchMetadata>[] inputResults)
        {
            var mergedIndex = new MergedIndex();
            foreach(var inputResult in inputResults)
            {
                await mergedIndex.MergeAsync(inputResult);
            }

            var ranking = _indexer.Rank(queryString, mergedIndex.Entries);

            var inputQueues = inputResults
                .Select(result => new Queue<string>(result.Select(entry => entry.Identity.Id)))
                .ToArray();

            var enqueued = new HashSet<string>();
            var outputQueue = new Queue<IPackageSearchMetadata>(mergedIndex.Entries.Count());

            while (inputQueues.Any(q => q.Count > 0))
            {
                foreach(var queue in inputQueues)
                {
                    // remove elements with no rank and already enqueued ones
                    while ((queue.Count > 0) && (enqueued.Contains(queue.Peek()) || !ranking.ContainsKey(queue.Peek())))
                    {
                        queue.Dequeue();
                    }
                }

                var candidates = inputQueues.Where(q => q.Count > 0).Select(q => q.Peek()).ToArray();
                if (candidates.Length > 0)
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
            private readonly IDictionary<string, IPackageSearchMetadata> _index = new Dictionary<string, IPackageSearchMetadata>(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<IPackageSearchMetadata> Entries => _index.Values;

            public IPackageSearchMetadata this[string key] => _index[key];

            public async Task MergeAsync(IEnumerable<IPackageSearchMetadata> result)
            {
                foreach (var entry in result)
                {
                    IPackageSearchMetadata value;
                    if (_index.TryGetValue(entry.Identity.Id, out value))
                    {
                        _index[entry.Identity.Id] = await MergeEntriesAsync(value, entry);
                    }
                    else
                    {
                        _index.Add(entry.Identity.Id, entry);
                    }
                }
            }

            private static async Task<IPackageSearchMetadata> MergeEntriesAsync(IPackageSearchMetadata lhs, IPackageSearchMetadata rhs)
            {
                var newerEntry = (lhs.Identity.Version >= rhs.Identity.Version) ? lhs : rhs;
                var versions = await Task.WhenAll(lhs.GetVersionsAsync(), rhs.GetVersionsAsync());
                var mergedVersions = versions.SelectMany(v => v).Distinct().ToArray();
                return newerEntry.WithVersions(mergedVersions);
            }
        }
    }
}
