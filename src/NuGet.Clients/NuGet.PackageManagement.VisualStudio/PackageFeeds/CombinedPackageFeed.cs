// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio.PackageFeeds
{
    internal class CombinedPackageFeed : IPackageFeed
    {
        private readonly IPackageFeed _mainFeed;
        private readonly IPackageFeed _recommenderFeed;

        public CombinedPackageFeed(IPackageFeed mainFeed, IPackageFeed recommenderFeed)
        {
            _mainFeed = mainFeed ?? throw new ArgumentNullException(nameof(mainFeed));
            _recommenderFeed = recommenderFeed ?? throw new ArgumentNullException(nameof(recommenderFeed));
        }

        public bool IsMultiSource => _mainFeed.IsMultiSource;

        public Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            return _mainFeed.ContinueSearchAsync(continuationToken, cancellationToken);
        }

        public Task<SearchResult<IPackageSearchMetadata>> RefreshSearchAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            return _mainFeed.RefreshSearchAsync(refreshToken, cancellationToken);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> SearchAsync(string searchText, SearchFilter filter, CancellationToken cancellationToken)
        {
            SearchResult<IPackageSearchMetadata> mainFeedResults = await _mainFeed.SearchAsync(searchText, filter, cancellationToken);
            SearchResult<IPackageSearchMetadata> recommenderResults = await _recommenderFeed.SearchAsync(searchText, filter, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var equalityComparer = new IdentityIdEqualityComparer();
            IReadOnlyList<IPackageSearchMetadata> combinedResults = recommenderResults.Items.Union(mainFeedResults.Items, equalityComparer).ToList();
            return SearchResult.FromItems(combinedResults);
        }

        private class IdentityIdEqualityComparer : IEqualityComparer<IPackageSearchMetadata>
        {
            public bool Equals(IPackageSearchMetadata x, IPackageSearchMetadata y)
            {
                return x.Identity.Id.Equals(y.Identity.Id, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(IPackageSearchMetadata obj)
            {
                return obj.Identity.Id.GetHashCode();
            }
        }
    }
}
