﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// A package feed providing services of package enumeration of installed packages having updated versions in upstream source(s).
    /// </summary>
    internal class UpdatePackageFeed : PlainPackageFeedBase
    {
        private readonly IEnumerable<PackageIdentity> _installedPackages;
        private readonly IPackageMetadataProvider _metadataProvider;
        private readonly PackageSearchMetadataCache _cachedUpdates;
        private readonly Common.ILogger _logger;

        public UpdatePackageFeed(
            IEnumerable<PackageIdentity> installedPackages,
            IPackageMetadataProvider metadataProvider,
            PackageSearchMetadataCache cachedUpdates,
            Common.ILogger logger)
        {
            if (installedPackages == null)
            {
                throw new ArgumentNullException(nameof(installedPackages));
            }
            _installedPackages = installedPackages;

            if (metadataProvider == null)
            {
                throw new ArgumentNullException(nameof(metadataProvider));
            }
            _metadataProvider = metadataProvider;

            _cachedUpdates = cachedUpdates;

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _logger = logger;
        }

        public override async Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            var searchToken = continuationToken as FeedSearchContinuationToken;
            if (searchToken == null)
            {
                throw new InvalidOperationException("Invalid token");
            }

            var packagesWithUpdates = (_cachedUpdates?.IncludePrerelease == searchToken.SearchFilter.IncludePrerelease)
                ?
                    GetPackagesFromCache(searchToken.SearchString)
                :
                    await GetPackagesWithUpdatesAsync(searchToken.SearchString, searchToken.SearchFilter, cancellationToken);

            var items = packagesWithUpdates
                .Skip(searchToken.StartIndex)
                .ToArray();

            var result = SearchResult.FromItems(items);

            var loadingStatus = items.Length == 0
                ? LoadingStatus.NoItemsFound
                : LoadingStatus.NoMoreItems;
            result.SourceSearchStatus = new Dictionary<string, LoadingStatus>
            {
                { "Update", loadingStatus }
            };

            return result;
        }

        private IEnumerable<IPackageSearchMetadata> GetPackagesFromCache(string searchText)
        {
            return _cachedUpdates.Packages.Where(p => p.Identity.Id.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1);
        }

        private async Task<IEnumerable<IPackageSearchMetadata>> GetPackagesWithUpdatesAsync(string searchText, SearchFilter searchFilter, CancellationToken cancellationToken)
        {
            var packages = _installedPackages
                .GetEarliest()
                .Where(p => p.Id.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id);

            var latestItems = await TaskCombinators.ThrottledAsync(
                packages, 
                (p, t) => _metadataProvider.GetLatestPackageMetadataAsync(p, searchFilter.IncludePrerelease, t), 
                cancellationToken);

            var packagesWithUpdates = packages
                .Join(latestItems.Where(i => i != null),
                    p => p.Id,
                    m => m.Identity.Id,
                    (p, m) => new { cv = p.Version, m = m },
                    StringComparer.OrdinalIgnoreCase)
                .Where(j => VersionComparer.VersionRelease.Compare(j.cv, j.m.Identity.Version) < 0)
                .Select(j => j.m);

            return packagesWithUpdates;
        }
    }
}
