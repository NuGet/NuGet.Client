﻿using System;
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
        private readonly Logging.ILogger _logger;

        public UpdatePackageFeed(
            IEnumerable<PackageIdentity> installedPackages,
            IPackageMetadataProvider metadataProvider,
            Logging.ILogger logger)
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

            var packagesWithUpdates = await GetPackagesWithUpdatesAsync(searchToken.SearchString, searchToken.SearchFilter, cancellationToken);
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
