using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Indexing;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Consolidated live sources package feed enumerating packages and aggregating search results.
    /// </summary>
    internal class MultiSourcePackageFeed : IPackageFeed
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private const int PageSize = 25;

        private readonly IEnumerable<SourceRepository> _sourceRepositories;
        private readonly Logging.ILogger _logger;

        private class AggregatedContinuationToken : ContinuationToken
        {
            public string SearchString { get; set; }
            public IDictionary<string, ContinuationToken> SourceSearchCursors { get; set; } = new Dictionary<string, ContinuationToken>();
        }

        private class AggregatedRefreshToken : RefreshToken
        {
            public string SearchString { get; set; }
            public IDictionary<string, Task<SearchResult<IPackageSearchMetadata>>> SearchTasks { get; set; }
            public IDictionary<string, LoadingStatus> SourceSearchStatus { get; set; }
        }

        public MultiSourcePackageFeed(IEnumerable<SourceRepository> sourceRepositories, Logging.ILogger logger)
        {
            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            if (!sourceRepositories.Any())
            {
                throw new ArgumentException("Collection of source repositories cannot be empty", nameof(sourceRepositories));
            }
            _sourceRepositories = sourceRepositories.ToArray();

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _logger = logger;
        }

        public async Task<SearchResult<IPackageSearchMetadata>> SearchAsync(string searchText, SearchFilter filter, CancellationToken cancellationToken)
        {
            var searchTasks = _sourceRepositories
                .ToDictionary(r => r.PackageSource.Name, r => r.SearchAsync(searchText, filter, PageSize, cancellationToken));

            var ignored = searchTasks.Values
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            return await WaitForCompletionOrBailOutAsync(searchText, searchTasks, cancellationToken);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            var searchToken = continuationToken as AggregatedContinuationToken;
            if (searchToken == null || searchToken.SourceSearchCursors == null)
            {
                throw new InvalidOperationException("Invalid token");
            }

            var searchTasks = _sourceRepositories
                .Join(searchToken.SourceSearchCursors,
                    r => r.PackageSource.Name,
                    c => c.Key,
                    (r, c) => new { Repository = r, NextToken = c.Value })
                .ToDictionary(
                    j => j.Repository.PackageSource.Name,
                    j => j.Repository.SearchAsync(j.NextToken, PageSize, cancellationToken));

            var ignored = searchTasks.Values
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            return await WaitForCompletionOrBailOutAsync(searchToken.SearchString, searchTasks, cancellationToken);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> RefreshSearchAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            var searchToken = refreshToken as AggregatedRefreshToken;
            if (searchToken == null)
            {
                throw new InvalidOperationException("Invalid token");
            }

            return await WaitForCompletionOrBailOutAsync(searchToken.SearchString, searchToken.SearchTasks, cancellationToken);
        }

        private async Task<SearchResult<IPackageSearchMetadata>> WaitForCompletionOrBailOutAsync(
            string searchText,
            IDictionary<string, Task<SearchResult<IPackageSearchMetadata>>> searchTasks, 
            CancellationToken cancellationToken)
        {
            if (searchTasks.Count == 0)
            {
                return SearchResult.Empty<IPackageSearchMetadata>();
            }

            var aggregatedTask = Task.WhenAll(searchTasks.Values);

            RefreshToken refreshToken = null;
            if (aggregatedTask != await Task.WhenAny(aggregatedTask, Task.Delay(DefaultTimeout)))
            {
                refreshToken = new AggregatedRefreshToken
                {
                    SearchString = searchText,
                    SearchTasks = searchTasks,
                    RetryAfter = DefaultTimeout
                };
            }

            var partitionedTasks = searchTasks
                .ToLookup(t => t.Value.Status == TaskStatus.RanToCompletion);

            var completedOnly = partitionedTasks[true];

            SearchResult<IPackageSearchMetadata> aggregated;
            if (completedOnly.Any())
            {
                var results = await Task.WhenAll(completedOnly.Select(kv => kv.Value));
                aggregated = await AggregateSearchResultsAsync(searchText, results);
            }
            else
            {
                aggregated = SearchResult.Empty<IPackageSearchMetadata>();
            }

            aggregated.RefreshToken = refreshToken;

            var notCompleted = partitionedTasks[false];
            if (notCompleted.Any())
            {
                aggregated.SourceSearchStatus
                    .AddRange(notCompleted
                        .ToDictionary(kv => kv.Key, kv => GetLoadingStatus(kv.Value.Status)));
            }

            return aggregated;
        }

        private static LoadingStatus GetLoadingStatus(TaskStatus taskStatus)
        {
            switch(taskStatus)
            {
                case TaskStatus.Canceled:
                    return LoadingStatus.Cancelled;
                case TaskStatus.Created:
                case TaskStatus.RanToCompletion:
                case TaskStatus.Running:
                case TaskStatus.WaitingForActivation:
                case TaskStatus.WaitingForChildrenToComplete:
                case TaskStatus.WaitingToRun:
                    return LoadingStatus.Loading;
                case TaskStatus.Faulted:
                    return LoadingStatus.ErrorOccured;
                default:
                    return LoadingStatus.Unknown;
            }
        }

        private async Task<SearchResult<IPackageSearchMetadata>> AggregateSearchResultsAsync(
            string searchText, 
            IEnumerable<SearchResult<IPackageSearchMetadata>> results)
        {
            SearchResult<IPackageSearchMetadata> result;

            var nonEmptyResults = results.Where(r => r.Any()).ToArray();
            if (nonEmptyResults.Length == 0)
            {
                result = SearchResult.Empty<IPackageSearchMetadata>();
            }
            else if (nonEmptyResults.Length == 1)
            {
                result = SearchResult.FromItems(nonEmptyResults[0].Items);
            }
            else
            {
                var indexer = new LuceneSearchResultsIndexer();
                var aggregator = new SearchResultsAggregator(indexer);
                var aggregatedItems = await aggregator.AggregateAsync(
                    searchText, nonEmptyResults.Select(r => r.Items).ToArray());
                result = SearchResult.FromItems(aggregatedItems.ToArray());
            }

            result.SourceSearchStatus = results
                .SelectMany(r => r.SourceSearchStatus)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var cursors = results
                .Where(r => r.NextToken != null)
                .ToDictionary(r => r.SourceSearchStatus.Single().Key, r => r.NextToken);

            if (cursors.Keys.Any())
            {
                result.NextToken = new AggregatedContinuationToken
                {
                    SearchString = searchText,
                    SourceSearchCursors = cursors
                };
            }

            return result;
        }

        private void LogError(Task task)
        {
            try
            {
                foreach (var ex in task.Exception.Flatten().InnerExceptions)
                {
                    _logger.LogError(ex.ToString());
                }
            }
            catch { }
        }
    }
}
