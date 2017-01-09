﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
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
        private readonly NuGetProject[] _projects;

        public UpdatePackageFeed(
            IEnumerable<PackageIdentity> installedPackages,
            IPackageMetadataProvider metadataProvider,
            NuGetProject[] projects,
            PackageSearchMetadataCache optionalCachedUpdates,
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

            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            _projects = projects;

            _cachedUpdates = optionalCachedUpdates;

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
                ["Update"] = loadingStatus
            };

            return result;
        }

        private IEnumerable<IPackageSearchMetadata> GetPackagesFromCache(string searchText)
        {
            return _cachedUpdates.Packages.Where(p => p.Identity.Id.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1);
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> GetPackagesWithUpdatesAsync(string searchText, SearchFilter searchFilter, CancellationToken cancellationToken)
        {
            var packages = _installedPackages
                .GetEarliest()
                .Where(p => p.Id.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id);

            // Prefetch metadata for all installed packages
            var prefetch = await TaskCombinators.ThrottledAsync(
                packages,
                (p, t) => _metadataProvider.GetPackageMetadataListAsync(p.Id, searchFilter.IncludePrerelease, includeUnlisted: false, cancellationToken: t),
                cancellationToken);

            // Flatten the result list
            var prefetchedPackages = prefetch
                .Where(p => p != null)
                .SelectMany(p => p)
                .ToArray();

            // Traverse all projects and determine packages with updates
            var packagesWithUpdates = new List<IPackageSearchMetadata>();
            foreach(var project in _projects)
            {
                var installed = await project.GetInstalledPackagesAsync(cancellationToken);
                foreach (var installedPackage in installed)
                {
                    var installedVersion = installedPackage.PackageIdentity.Version;
                    var allowedVersions = installedPackage.AllowedVersions ?? VersionRange.All;

                    // filter packages based on current package identity
                    var allPackages = prefetchedPackages
                        .Where(p => StringComparer.OrdinalIgnoreCase.Equals(
                            p.Identity.Id,
                            installedPackage.PackageIdentity.Id))
                        .ToArray();

                    // and allowed versions
                    var allowedPackages = allPackages
                        .Where(p => allowedVersions.Satisfies(p.Identity.Version));

                    // peek the highest available
                    var highest = allowedPackages
                        .OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease)
                        .FirstOrDefault();

                    if (highest != null &&
                        VersionComparer.VersionRelease.Compare(installedVersion, highest.Identity.Version) < 0)
                    {
                        packagesWithUpdates.Add(highest.WithVersions(ToVersionInfo(allPackages)));
                    }
                }
            }

            // select the earliest package update candidates
            var uniquePackageIds = packagesWithUpdates
                .Select(p => p.Identity)
                .GetEarliest();

            // get unique list of package metadata as similar updates may come from different projects
            var uniquePackages = uniquePackageIds
                .GroupJoin(
                    packagesWithUpdates,
                    id => id,
                    p => p.Identity,
                    (id, pl) => pl.First());

            return uniquePackages.ToArray();
        }

        private static IEnumerable<VersionInfo> ToVersionInfo(IEnumerable<IPackageSearchMetadata> packages)
        {
            return packages?
                .OrderByDescending(m => m.Identity.Version, VersionComparer.VersionRelease)
                .Select(m => new VersionInfo(m.Identity.Version, m.DownloadCount)
                {
                    PackageSearchMetadata = m
                });
        }
    }
}
