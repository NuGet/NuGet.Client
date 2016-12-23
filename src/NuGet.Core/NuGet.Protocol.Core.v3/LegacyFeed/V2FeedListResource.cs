// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.LegacyFeed
{
    public class V2FeedListResource : ListResource
    {
        private readonly ILegacyFeedCapabilityResource _feedCapabilities;
        private readonly IV2FeedParser _feedParser;
        private const int Take = 30;

        public V2FeedListResource(IV2FeedParser feedParser, ILegacyFeedCapabilityResource feedCapabilities)
        {
            _feedParser = feedParser;
            _feedCapabilities = feedCapabilities;
        }

        public override Task<IEnumerableAsync<IPackageSearchMetadata>> ListAsync(
            string searchTerm,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            ILogger logger,
            CancellationToken token)
        {
            return ListRangeAsync(searchTerm, prerelease, allVersions, includeDelisted, 0, Take, logger, token);
        }

        private async Task<IEnumerableAsync<IPackageSearchMetadata>> ListRangeAsync(string searchTerm,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            int skip,
            int take,
            ILogger logger,
            CancellationToken token)
        {
            var isSearchSupported = await _feedCapabilities.SupportsSearchAsync(logger, token);

            if (isSearchSupported)
            {

                if (allVersions)
                {
                    var filter = new SearchFilter(includePrerelease: true);
                    filter.OrderBy = SearchOrderBy.Id;
                    // whether prerelease is included should not matter as allVersions precedes it
                    filter.IncludeDelisted = includeDelisted;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take,isSearchSupported,
                        logger, token);
                }

                var supportsIsAbsoluteLatestVersion =
                    await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.OrderBy = SearchOrderBy.Id;
                    filter.IncludeDelisted = includeDelisted;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take, isSearchSupported,
                        logger, token);

                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    filter.OrderBy = SearchOrderBy.Id;
                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take, isSearchSupported,
                        logger, token);


                }
            }
            else
            {
                if (allVersions)
                {
                    var filter = new SearchFilter(includePrerelease: true);
                    // whether prerelease is included should not matter as allVersions precedes it
                    filter.IncludeDelisted = includeDelisted;
                    filter.OrderBy = SearchOrderBy.Id;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take, isSearchSupported, logger, token);
                }

                var supportsIsAbsoluteLatestVersion =
                    await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    filter.OrderBy = SearchOrderBy.Id;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take, isSearchSupported, logger, token);
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    filter.OrderBy = SearchOrderBy.Id;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTerm, filter, skip, take, isSearchSupported, logger, token);
                }

            }
        }
    }

    class EnumerableAsync<T> : IEnumerableAsync<T>
    {
        private readonly SearchFilter _filter;
        private readonly ILogger _logger;
        private  readonly string _searchTerm;
        private readonly int _skip;
        private readonly int _take;
        private readonly CancellationToken _token;
        private readonly IV2FeedParser _feedParser;
        private readonly bool _isSearchAvailable;


        public EnumerableAsync(IV2FeedParser feedParser, string searchTerm, SearchFilter filter, int skip, int take, bool isSearchAvailable, ILogger logger, CancellationToken token)
        {
            _feedParser = feedParser;
            _searchTerm = searchTerm;
            _filter = filter;
            _skip = skip;
            _take = take;
            _isSearchAvailable = isSearchAvailable;
            _logger = logger;
            _token = token;
        }

        public IEnumeratorAsync<T> GetEnumeratorAsync()
        {
            return (IEnumeratorAsync<T>)new EnumeratorAsync(_feedParser, _searchTerm, _filter, _skip, _take,_isSearchAvailable, _logger, _token);
        }
    }

    internal class EnumeratorAsync : IEnumeratorAsync<IPackageSearchMetadata>
    {
        private readonly SearchFilter _filter;
        private readonly ILogger _logger;
        private readonly string _searchTerm;
        private int _skip;
        private readonly int _take;
        private readonly CancellationToken _token;
        private readonly IV2FeedParser _feedParser;
        private readonly bool _isSearchAvailable;


        private IEnumerator<IPackageSearchMetadata> _currentEnumerator;
        private V2FeedPage _currentPage;

        public EnumeratorAsync(IV2FeedParser feedParser, string searchTerm, SearchFilter filter, int skip, int take, bool isSearchAvailable,
            ILogger logger, CancellationToken token)
        {
            _feedParser = feedParser;
            _searchTerm = searchTerm;
            _filter = filter;
            _skip = skip;
            _take = take;
            _isSearchAvailable = isSearchAvailable;
            _logger = logger;
            _token = token;
        }

        public IPackageSearchMetadata Current
        {
            get
            {
                if (_currentEnumerator != null)
                    return _currentEnumerator.Current;
                else return null;
            }
        }

        public async Task<bool> MoveNextAsync()
        {
            if (_currentPage == null)
            {


                _currentPage = _isSearchAvailable
                    ? await _feedParser.GetSearchPageAsync(_searchTerm, _filter, _skip, _take, _logger, _token)
                    : await _feedParser.GetPackagesPageAsync(_searchTerm, _filter, _skip, _take, _logger, _token);
                var results = _currentPage.Items.GroupBy(p => p.Id)
                    .Select(group => group.OrderByDescending(p => p.Version).First())
                    .Select(
                        package =>
                            V2FeedUtilities.CreatePackageSearchResult(package, _filter,
                                (V2FeedParser)_feedParser, _logger, _token));
                var enumerator = results.GetEnumerator();
                _currentEnumerator = enumerator;
                _currentEnumerator.MoveNext();
                return true;
            }
            else
            {
                if (!_currentEnumerator.MoveNext())
                {
                    if (_currentPage.Items.Count != _take) // Last page not filled completely, no more pages left
                    {
                        return false;
                    }
                    _skip += _take;
                    _currentPage = _isSearchAvailable
                                        ? await _feedParser.GetSearchPageAsync(_searchTerm, _filter, _skip, _take, _logger, _token)
                                        : await _feedParser.GetPackagesPageAsync(_searchTerm, _filter, _skip, _take, _logger, _token);
                    var results = _currentPage.Items.GroupBy(p => p.Id)
                        .Select(group => group.OrderByDescending(p => p.Version).First())
                        .Select(
                            package =>
                                V2FeedUtilities.CreatePackageSearchResult(package, _filter,
                                    (V2FeedParser)_feedParser, _logger, _token));
                    var enumerator = results.GetEnumerator();
                    _currentEnumerator = enumerator;
                    _currentEnumerator.MoveNext();
                    return true;
                }
                else
                {
                    return true;
                }

            }
        }
    }
}
