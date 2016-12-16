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
                    filter.IncludeDelisted = includeDelisted;

                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take,
                        logger, token);

                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
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


                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take, logger, token);

                }

                var supportsIsAbsoluteLatestVersion =
                    await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    return new EnumerableAsync<IPackageSearchMetadata>(_feedParser, searchTime, filter, skip, take, logger, token);
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
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
        private SearchFilter filter;
        private ILogger logger;
        private string searchTime;
        private int skip;
        private int take;
        private CancellationToken token;
        private IV2FeedParser _feedParser;

        private IEnumerator<IPackageSearchMetadata> currentEnumerator;
        private V2FeedPage currentPage;

        public EnumeratorAsync(IV2FeedParser _feedParser, string searchTime, SearchFilter filter, int skip, int take,
            ILogger logger, CancellationToken token)
        {
            this._feedParser = _feedParser;
            this.searchTime = searchTime;
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
                if (currentEnumerator == null)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    return currentEnumerator.Current; //This too might throw IOE
                }
            }
        }

        public async Task<bool> MoveNextAsync()
        {
            if (currentPage == null)
            {

                currentPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                var results = currentPage.Items.GroupBy(p => p.Id)
                    .Select(group => group.OrderByDescending(p => p.Version).First())
                    .Select(
                        package =>
                            PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter,
                                (V2FeedParser)_feedParser, logger, token));
                var enumerator = results.GetEnumerator();
                currentEnumerator = enumerator;
                return true;
            }
            else
            {
                if (!currentEnumerator.MoveNext())
                {
                    string nextUri = currentPage.NextUri;

                    if (nextUri == null)
                    {
                        return false;
                    }
                    currentPage = await _feedParser.QueryV2FeedAsync(nextUri, null, take, false, logger, token);
                    var results = currentPage.Items.GroupBy(p => p.Id)
                        .Select(group => group.OrderByDescending(p => p.Version).First())
                        .Select(
                            package =>
                                PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter,
                                    (V2FeedParser)_feedParser, logger, token));
                    var enumerator = results.GetEnumerator();
                    currentEnumerator = enumerator;
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
