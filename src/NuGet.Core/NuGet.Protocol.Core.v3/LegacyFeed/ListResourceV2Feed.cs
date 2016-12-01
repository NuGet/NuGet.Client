// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using  NuGet.Protocol.Core.Types;

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
            return ListRangeAsync(searchTime, prerelease, allVersions, includeDelisted, 0, take, logger, token);
        }

        private  async Task<IEnumerable<IPackageSearchMetadata>> ListRangeAsync(string searchTime,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            int skip,
            int take,
            ILogger logger,
            CancellationToken token)
        {
            //TODO NK - how to get the allVersions
            var isSearchSupported = await _feedCapabilities.SupportsSearchAsync(logger, token);
            if (isSearchSupported)
            {
                var supportsIsAbsoluteLatestVersion = await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);
                if (prerelease && supportsIsAbsoluteLatestVersion)
                {
                    var filter = new SearchFilter(includePrerelease: true,
                        filter: SearchFilterType.IsAbsoluteLatestVersion);
                    filter.IncludeDelisted = includeDelisted;
                    var v2FeedPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                            .Select(group => group.OrderByDescending(p => p.Version).First()) //TODO NK - fix this shit
                            .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }
                else
                {
                    var filter = new SearchFilter(includePrerelease: false,
                        filter: SearchFilterType.IsLatestVersion);
                    var v2FeedPage = await _feedParser.GetSearchPageAsync(searchTime, filter, skip, take, logger, token);
                    var results = v2FeedPage.Items.GroupBy(p => p.Id)
                           .Select(group => group.OrderByDescending(p => p.Version).First()) //TODO NK - fix this shit
                           .Select(package => PackageSearchResourceV2Feed.CreatePackageSearchResult(package, filter, (V2FeedParser)_feedParser, logger, token));
                    return results;
                }

            }
            else
            {

                var supportsIsAbsoluteLatestVersion = await _feedCapabilities.SupportsIsAbsoluteLatestVersionAsync(logger, token);
                //               _feedParser.GetSearchPageAsync()

            }

            return null;
        }

        private IPackageSearchMetadata CreatePackageSearchResult(
           V2FeedPackageInfo package,
           SearchFilter filter,
           Common.ILogger log,
           CancellationToken cancellationToken)
        {
            var metadata = new PackageSearchMetadataV2Feed(package);
            return metadata
                .WithVersions(() => GetVersions(package, filter, log, cancellationToken));
        }
        // this should be async, removed just to fix compile erros for now
        public  Task<IEnumerable<VersionInfo>> GetVersions(
            V2FeedPackageInfo package,
            SearchFilter filter,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //// apply the filters to the version list returned
            //var packages = await _feedParser.FindPackagesByIdAsync(
            //    package.Id,
            //    filter.IncludeDelisted,
            //    filter.IncludePrerelease,
            //    log,
            //    cancellationToken);

            //var uniqueVersions = new HashSet<NuGetVersion>();
            var results = new List<VersionInfo>();

            //foreach (var versionPackage in packages.OrderByDescending(p => p.Version))
            //{
            //    if (uniqueVersions.Add(versionPackage.Version))
            //    {
            //        var versionInfo = new VersionInfo(versionPackage.Version, versionPackage.DownloadCount)
            //        {
            //            PackageSearchMetadata = new PackageSearchMetadataV2Feed(versionPackage)
            //        };

            //        results.Add(versionInfo);
            //    }
            //}
            return null;
        }
        // this needs to have the async keyword
        private  Task<IEnumerable<IPackageSearchMetadata>> ListWithSearchAsync(
            string searchTime,
            bool prerelease,
            bool allVersions,
            bool includeDelisted,
            CancellationToken token)
        {
            return null;
        }
    }

    public class FibonacciEnumerator : IEnumerator<IPackageSearchMetadata>
    {
        public FibonacciEnumerator()
        {
            Reset();
        }

        IPackageSearchMetadata Last { get; set; }
        public IPackageSearchMetadata Current { get; private set; }
        object IEnumerator.Current { get { return Current; } }

        public void Dispose() { /*do nothing*/}

        public void Reset()
        {
            Current = null;
        }

        public bool MoveNext()
        {
            return true;
            //if (Current ==null)
            //{
            //    //State after first call to MoveNext() before the first element is read
            //    //Fibonacci is defined to start with 0.
            //    Current = 0;
            //}
            //else if (Current == 0)
            //{
            //    //2nd element in the Fibonacci series is defined to be 1.
            //    Current = 1;
            //}
            //else
            //{
            //    //Fibonacci infinite series algorithm.
            //    BigInteger next = Current + Last;
            //    Last = Current;
            //    Current = next;
            //}
            ////infinite series. there is always another.
            //return true;
        }
    }
}
