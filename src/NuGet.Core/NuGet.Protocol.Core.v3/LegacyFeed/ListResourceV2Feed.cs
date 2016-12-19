// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ListResourceV2Feed : ListResource
    {
        private readonly ILegacyFeedCapabilityResource _feedCapabilities;
        private readonly IV2FeedParser _feedParser;

        public ListResourceV2Feed(IV2FeedParser feedParser, ILegacyFeedCapabilityResource feedCapabilities)
        {
            _feedParser = feedParser;
            _feedCapabilities = feedCapabilities;
        }

        public override Task<IEnumerableAsync<IPackageSearchMetadata>> ListAsync(
            string searchTime,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            ILogger logger,
            CancellationToken token)
        {
            var take = 30; // TODO NK - Should this be customizable? 
            return ListRangeAsync(searchTime, prerelease, allVersions, includeDelisted, 0, take, logger, token);
        }

        private async Task<IEnumerableAsync<IPackageSearchMetadata>> ListRangeAsync(string searchTime,
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

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take,
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

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take,
                        logger, token);

                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    filter.OrderBy = SearchOrderBy.Id;
                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take,
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

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take, logger, token);

                }

                var supportsIsAbsoluteLatestVersion =
                    await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    filter.OrderBy = SearchOrderBy.Id;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take, logger, token);
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    filter.OrderBy = SearchOrderBy.Id;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take, logger, token);
                }

            }
        }
    }

    class EnumerableAsync<T> : IEnumerableAsync<T>
    {
        private SearchFilter filter;
        private ILogger logger;
        private string searchTime;
        private int skip;
        private int take;
        private CancellationToken token;
        private IV2FeedParser _feedParser;

        public EnumerableAsync(IV2FeedParser _feedParser, string searchTime, SearchFilter filter, int skip, int take, ILogger logger, CancellationToken token)
        {
            this._feedParser = _feedParser;
            this.searchTime = searchTime;
            this.filter = filter;
            this.skip = skip;
            this.take = take;
            this.logger = logger;
            this.token = token;
        }

        public IEnumeratorAsync<T> GetEnumeratorAsync()
        {
            return (IEnumeratorAsync<T>)new EnumeratorAsync(_feedParser, searchTime, filter, skip, take, logger, token);
        }
    }

    internal class EnumeratorAsync : IEnumeratorAsync<IPackageSearchMetadata>
    {
        private readonly SearchFilter filter;
        private readonly ILogger logger;
        private readonly string _searchTime;
        private readonly int skip;
        private readonly int take;
        private readonly CancellationToken token;
        private readonly IV2FeedParser _feedParser;

        private IEnumerator<IPackageSearchMetadata> _currentEnumerator;
        private V2FeedPage _currentPage;

        public EnumeratorAsync(IV2FeedParser feedParser, string searchTime, SearchFilter filter, int skip, int take,
            ILogger logger, CancellationToken token)
        {
            this._feedParser = feedParser;
            this._searchTime = searchTime;
            this.filter = filter;
            this.skip = skip;
            this.take = take;
            this.logger = logger;
            this.token = token;
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

                _currentPage = await _feedParser.GetSearchPageAsync(_searchTime, filter, skip, take, logger, token);
                var results = _currentPage.Items.GroupBy(p => p.Id)
                    .Select(group => group.OrderByDescending(p => p.Version).First())
                    .Select(
                        package =>
                            PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter,
                                (V2FeedParser)_feedParser, logger, token));
                var enumerator = results.GetEnumerator();
                _currentEnumerator = enumerator;
                _currentEnumerator.MoveNext();
                return true;
            }
            else
            {
                if (!_currentEnumerator.MoveNext())
                {
                    string nextUri = _currentPage.NextUri;

                    if (nextUri == null)
                    {
                        return false;
                    }
                    _currentPage = await _feedParser.QueryV2FeedAsync(nextUri, null, take, false, logger, token);
                    var results = _currentPage.Items.GroupBy(p => p.Id)
                        .Select(group => group.OrderByDescending(p => p.Version).First())
                        .Select(
                            package =>
                                PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter,
                                    (V2FeedParser)_feedParser, logger, token));
                    var enumerator = results.GetEnumerator();
                    _currentEnumerator = enumerator;
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
