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

        public override Task<IEnumerable<IPackageSearchMetadata>> ListAsync(
            string searchTime,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            ILogger logger,
            CancellationToken token)
        {
            var take = 20;
            var skip = 0;
            return ListRangeAsync(searchTime, prerelease, allVersions, includeDelisted, skip, take, logger, token);
        }

        private async Task<IEnumerable<IPackageSearchMetadata>> ListRangeAsync(string searchTime,
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
                    var filter = new SearchFilter(includePrerelease:true); // whether prerelease is included should not matter as allVersions precedes it
                    filter.IncludeDelisted = includeDelisted;

                    var v2FeedPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                            .Select(group => group.OrderByDescending(p => p.Version).First()) 
                            .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;

                }

                var supportsIsAbsoluteLatestVersion = await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    var v2FeedPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                            .Select(group => group.OrderByDescending(p => p.Version).First())
                            .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    var v2FeedPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                           .Select(group => group.OrderByDescending(p => p.Version).First())
                           .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }

            }
            else
            {
                if (allVersions)
                {
                    var filter = new SearchFilter(includePrerelease: true); // whether prerelease is included should not matter as allVersions precedes it
                    filter.IncludeDelisted = includeDelisted;

                    var v2FeedPage = await _feedParser.GetPackagesPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                            .Select(group => group.OrderByDescending(p => p.Version).First())
                            .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;

                }

                var supportsIsAbsoluteLatestVersion = await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);

                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    var v2FeedPage = await _feedParser.GetPackagesPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                            .Select(group => group.OrderByDescending(p => p.Version).First())
                            .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    var v2FeedPage = await _feedParser.GetPackagesPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                           .Select(group => group.OrderByDescending(p => p.Version).First())
                           .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }

            }
        }
    }
}
