// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            foreach (var inputResult in inputResults)
            {
                await mergedIndex.MergeAsync(inputResult);
            }

            var ranking = _indexer.Rank(queryString, mergedIndex.Entries);

            var inputQueues = inputResults
                .Select(result => new Queue<string>(
                    _indexer.ProcessUnrankedEntries(result, ranking).Select(entry => entry.Identity.Id)))
                .ToArray();

            var enqueued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputQueue = new Queue<IPackageSearchMetadata>(mergedIndex.Entries.Count());

            var queues = inputQueues.Where(q => q.Count > 0);
            while (queues.Count() > 1)
            {
                var candidates = queues.Select(q => q.Peek()).ToArray();

                var winnerRank = candidates.Max(x => ranking[x]);
                var winners = candidates.Where(x => ranking[x] == winnerRank);
                foreach (var winner in winners.Where(w => !enqueued.Contains(w)))
                {
                    enqueued.Add(winner);
                    outputQueue.Enqueue(mergedIndex[winner]);
                }

                foreach (var queue in queues)
                {
                    // remove elements with no rank and already enqueued ones
                    while ((queue.Count > 0) && (enqueued.Contains(queue.Peek()) || !ranking.ContainsKey(queue.Peek())))
                    {
                        queue.Dequeue();
                    }
                }
            }

            // append tail elements from the last queue (should be one or none)
            // in most cases input queues are unbalanced, i.e. contain different amount of search results.
            // in that scenario merging algorithm ends leaving a "tail" in a longer queue.
            // although in certain rare edge cases it might empty all queues at once.
            if (queues.Any())
            {
                foreach (var entry in queues.Single().Where(e => !enqueued.Contains(e)))
                {
                    // don't have to update enqueued as this is the last queue
                    outputQueue.Enqueue(mergedIndex[entry]);
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
                var mergedVersions = versions
                    .SelectMany(v => v) // flatten a list of two lists
                    .GroupBy(v => v.Version) // group all by version
                    .Select(group => group.First()) // select first VersionInfo for each version
                    .ToArray(); // force execution

                return newerEntry.WithVersions(mergedVersions);
            }
        }
    }
}
