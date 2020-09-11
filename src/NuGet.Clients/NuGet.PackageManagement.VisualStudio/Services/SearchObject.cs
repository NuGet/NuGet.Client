// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class SearchObject
    {
        private readonly IPackageFeed _mainFeed;
        private IPackageFeed? _recommenderFeed;
        private SearchResult<IPackageSearchMetadata>? _lastMainFeedSearchResult;

        public SearchObject(IPackageFeed mainFeed, IPackageFeed? recommenderFeed)
        {
            Assumes.NotNull(mainFeed);

            _mainFeed = mainFeed;
            _recommenderFeed = recommenderFeed;
        }

        public async ValueTask<SearchResultContextInfo> SearchAsync(string searchText, SearchFilter filter, bool useRecommender, CancellationToken cancellationToken)
        {
            SearchResult<IPackageSearchMetadata>? mainFeedResult = await _mainFeed.SearchAsync(searchText, filter, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            SearchResult<IPackageSearchMetadata>? recommenderFeedResults = null;
            if (useRecommender && _recommenderFeed != null)
            {
                recommenderFeedResults = await _recommenderFeed.SearchAsync(searchText, filter, cancellationToken);
            }

            _lastMainFeedSearchResult = mainFeedResult; // Store this so we can ContinueSearch, we don't store recommended as we only do that on the first search

            if (recommenderFeedResults != null)
            {
                // remove duplicated recommended packages from the browse results
                var recommendedIds = recommenderFeedResults.Items.Select(item => item.Identity.Id);

                IReadOnlyCollection<PackageSearchMetadataContextInfo> filteredMainFeedResult =
                        mainFeedResult.Items.Where(item => !recommendedIds.Contains(item.Identity.Id))
                        .Select(mainFeedPackageSearchMetadata => PackageSearchMetadataContextInfo.Create(mainFeedPackageSearchMetadata)).ToList();

                IList<PackageSearchMetadataContextInfo> recommendedPackageSearchMetadataContextInfo =
                    recommenderFeedResults.Items.Select(
                        packageSearchMetadata =>
                        {
                            var recommendedPackageSearchMetadataContextInfo = PackageSearchMetadataContextInfo.Create(packageSearchMetadata);
                            recommendedPackageSearchMetadataContextInfo.IsRecommended = true;
                            recommendedPackageSearchMetadataContextInfo.RecommenderVersion = (_recommenderFeed as RecommenderPackageFeed)?.VersionInfo;
                            return recommendedPackageSearchMetadataContextInfo;
                        }).ToList();

                recommendedPackageSearchMetadataContextInfo.AddRange(filteredMainFeedResult);
                return new SearchResultContextInfo(recommendedPackageSearchMetadataContextInfo.ToList(), mainFeedResult.SourceSearchStatus, mainFeedResult.NextToken != null)
                {
                    OperationId = _lastMainFeedSearchResult.OperationId
                };
            }

            IReadOnlyCollection<PackageSearchMetadataContextInfo> packageSearchMetadataContextInfoCollection =
                mainFeedResult.Items.Select(mainFeedPackageSearchMetadata => PackageSearchMetadataContextInfo.Create(mainFeedPackageSearchMetadata)).ToList();
            return new SearchResultContextInfo(packageSearchMetadataContextInfoCollection, mainFeedResult.SourceSearchStatus, mainFeedResult.NextToken != null)
            {
                OperationId = mainFeedResult.OperationId
            };
        }

        public async ValueTask<SearchResultContextInfo> RefreshSearchAsync(CancellationToken cancellationToken)
        {
            Assumes.NotNull(_lastMainFeedSearchResult);

            var refreshSearchResult = await _mainFeed.RefreshSearchAsync(_lastMainFeedSearchResult.RefreshToken, cancellationToken);
            _lastMainFeedSearchResult = refreshSearchResult;

            IReadOnlyCollection<PackageSearchMetadataContextInfo> packageItems = _lastMainFeedSearchResult.Items.Select(a => PackageSearchMetadataContextInfo.Create(a)).ToList();
            return new SearchResultContextInfo(packageItems, refreshSearchResult.SourceSearchStatus, refreshSearchResult.NextToken != null)
            {
                OperationId = _lastMainFeedSearchResult.OperationId
            };
        }

        public async ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetAllPackagesAsync(SearchFilter searchFilter, CancellationToken cancellationToken)
        {
            var packages = new List<PackageSearchMetadataContextInfo>();
            do
            {
                var searchResult = await SearchAsync(string.Empty, searchFilter, false, cancellationToken);
                if (_lastMainFeedSearchResult?.RefreshToken != null)
                {
                    searchResult = await RefreshSearchAsync(cancellationToken);
                }

                packages.AddRange(searchResult.PackageSearchItems);
            } while (_lastMainFeedSearchResult?.NextToken != null);

            return packages;
        }

        public async ValueTask<SearchResultContextInfo> ContinueSearchAsync(CancellationToken cancellationToken)
        {
            Assumes.NotNull(_lastMainFeedSearchResult);

            if(_lastMainFeedSearchResult.NextToken == null)
            {
                return new SearchResultContextInfo()
                {
                    OperationId = _lastMainFeedSearchResult.OperationId,
                };
            }

            var continueSearchResult = await _mainFeed.ContinueSearchAsync(_lastMainFeedSearchResult.NextToken, cancellationToken);
            _lastMainFeedSearchResult = continueSearchResult;

            IReadOnlyCollection<PackageSearchMetadataContextInfo> packageItems = _lastMainFeedSearchResult.Items.Select(a => PackageSearchMetadataContextInfo.Create(a)).ToList();
            return new SearchResultContextInfo(packageItems, continueSearchResult.SourceSearchStatus, continueSearchResult.NextToken != null)
            {
                OperationId = _lastMainFeedSearchResult.OperationId
            };
        }

        public async ValueTask<int> GetTotalCountAsync(int maxCount, SearchFilter filter, CancellationToken cancellationToken)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;

            ActivityCorrelationId.StartNew();

            int totalCount = 0;
            ContinuationToken? nextToken;
            do
            {
                var searchResult = await _mainFeed.SearchAsync(string.Empty, filter, cancellationToken);
                while (searchResult.RefreshToken != null)
                {
                    searchResult = await _mainFeed.RefreshSearchAsync(searchResult.RefreshToken, cancellationToken);
                }
                totalCount += searchResult.Items?.Count() ?? 0;
                nextToken = searchResult.NextToken;
            } while (nextToken != null && totalCount < maxCount);

            return totalCount;
        }
    }
}
