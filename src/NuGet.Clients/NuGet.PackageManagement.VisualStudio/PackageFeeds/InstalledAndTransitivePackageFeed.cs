// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class InstalledAndTransitivePackageFeed : InstalledPackageFeed
    {
        private readonly IEnumerable<PackageCollectionItem> _transitivePackages;

        public InstalledAndTransitivePackageFeed(IEnumerable<PackageCollectionItem> installedPackages, IEnumerable<PackageCollectionItem> transitivePackages, IPackageMetadataProvider metadataProvider)
            : base(installedPackages, metadataProvider)
        {
            _transitivePackages = transitivePackages ?? throw new ArgumentNullException(nameof(transitivePackages));
        }

        /// <inheritdoc cref="IPackageFeed.ContinueSearchAsync(ContinuationToken, CancellationToken)" />
        public override async Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            var searchToken = continuationToken as FeedSearchContinuationToken ?? throw new InvalidOperationException(Strings.Exception_InvalidContinuationToken);
            cancellationToken.ThrowIfCancellationRequested();

            PackageCollectionItem[] installedFeedItems = _installedPackages.GetLatest();
            PackageCollectionItem[] matchesInstalled = PerformLookup(installedFeedItems, searchToken);

            // Remove transitive packages from project references
            IEnumerable<PackageCollectionItem> transitivePkgsWithOrigins = _transitivePackages
                .Where(transitivePkg => transitivePkg
                    .PackageReferences
                    .All(pkgRef => pkgRef is ITransitivePackageReferenceContextInfo trPkgRef && trPkgRef.TransitiveOrigins.Any()));

            // First, Search all transitive packages
            // If we first group transitive packages by Id, then, lookup, transitive package origins could not match Installed package version
            PackageCollectionItem[] matchesTransitive = PerformLookupCore(transitivePkgsWithOrigins, searchToken)
                // Then, group by package ID
                .GroupById()
                // Select latest version with matching transitive origin within installed packages collection
                // or fallback to latest transitive package version available if its transitive origins are not found
                .Select(group => GetLatestApplicableTransitivePackageVersion(group, matchesInstalled))
                .ToArray();

            IPackageSearchMetadata[] installedItems = await GetMetadataForPackagesAndSortAsync(matchesInstalled, searchToken.SearchFilter.IncludePrerelease, cancellationToken);
            IPackageSearchMetadata[] transitiveItems = await GetMetadataForPackagesAndSortAsync(matchesTransitive, searchToken.SearchFilter.IncludePrerelease, cancellationToken);
            IPackageSearchMetadata[] items = installedItems.Concat(transitiveItems).ToArray();

            return CreateResult(items);
        }

        internal override async Task<IPackageSearchMetadata> GetPackageMetadataAsync<T>(T identity, bool includePrerelease, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (identity is PackageCollectionItem pkgColItem)
            {
                IEnumerable<ITransitivePackageReferenceContextInfo> transitivePRs = pkgColItem.PackageReferences.OfType<ITransitivePackageReferenceContextInfo>();
                ITransitivePackageReferenceContextInfo transitivePR = transitivePRs.OrderByDescending(x => x.Identity.Version).FirstOrDefault();
                IReadOnlyCollection<PackageIdentity> transitiveOrigins = transitivePR?.TransitiveOrigins?.Select(to => to.Identity).ToArray() ?? Array.Empty<PackageIdentity>();
                if (transitiveOrigins.Any())
                {
                    // Get only local metadata. We don't want Deprecation and Vulnerabilities Metadata on Transitive packages
                    IPackageSearchMetadata packageMetadata = await _metadataProvider.GetOnlyLocalPackageMetadataAsync(pkgColItem, cancellationToken);
                    if (packageMetadata == null) // Edge case: local metadata not found
                    {
                        packageMetadata = PackageSearchMetadataBuilder.FromIdentity(pkgColItem).Build(); // create metadata object only with ID
                    }

                    return new TransitivePackageSearchMetadata(packageMetadata, transitiveOrigins);
                }
            }

            return await base.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
        }

        internal static PackageCollectionItem GetLatestApplicableTransitivePackageVersion(IGrouping<string, PackageCollectionItem> groupedPackagesById, IEnumerable<PackageCollectionItem> installedPkgs)
        {
            if (groupedPackagesById == null || !groupedPackagesById.Any())
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(groupedPackagesById));
            }
            if (installedPkgs == null)
            {
                throw new ArgumentNullException(nameof(installedPkgs));
            }
            if (groupedPackagesById
                    .Any(group => !group.PackageReferences.Any() // PackageReferences is empty
                        || group.PackageReferences.Any(pkgRef =>
                            pkgRef is not ITransitivePackageReferenceContextInfo tr // it is not a transitive package
                            || !tr.TransitiveOrigins.Any()))) // No transitive origin for a transitive package
            {
                throw new ArgumentException(Strings.InvalidCollectionElementType, nameof(groupedPackagesById));
            }

            IEnumerable<PackageCollectionItem> matchedOrigins = groupedPackagesById
                .Where(group => group.PackageReferences
                    .Cast<ITransitivePackageReferenceContextInfo>()
                    .Any(trPkgRef => trPkgRef.TransitiveOrigins.Any(trOrigin => installedPkgs.Contains(trOrigin.Identity))));

            if (matchedOrigins.Any())
            {
                // Return the latest transitive package with installed versions
                return matchedOrigins.OrderByDescending(pkgCollection => pkgCollection.Version).First();
            }

            // Otherwise, fallback on what's available
            return groupedPackagesById.OrderByDescending(pkgCollection => pkgCollection.Version).First();
        }
    }
}
