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

            // Remove transitive packages from project references. Those transitive packages don't have Transitive Origins
            IEnumerable<PackageCollectionItem> transitivePkgsWithOrigins = _transitivePackages
                .Where(t => t.PackageReferences.All(x => x is ITransitivePackageReferenceContextInfo y && y.TransitiveOrigins.Any()));

            // Get the metadata for the latest version of the installed packages and transitive packages
            var installedPackagesGrouped = _installedPackages.GroupById();
            var installedPackagesLatest = installedPackagesGrouped.Select(g => g.OrderByDescending(x => x.Version).First());

            var latestTransitiveGrouped = transitivePkgsWithOrigins.GroupById();
            var latestTransitivePackages = latestTransitiveGrouped.Select(g => g.OrderByDescending(x => x.Version).First());

            IPackageSearchMetadata[] installedItems = await GetMetadataForPackagesAndSortAsync(PerformLookup(installedPackagesLatest, searchToken), searchToken.SearchFilter.IncludePrerelease, cancellationToken);
            IPackageSearchMetadata[] transitiveItems = await GetMetadataForPackagesAndSortAsync(PerformLookup(latestTransitivePackages, searchToken), searchToken.SearchFilter.IncludePrerelease, cancellationToken);

            // Get metedata from identity for the rest of the installed and transitive packages
            var remainingInstalledItems = await GetMetadataFromIdentityForPackagesAndSortAsync(PerformLookup(installedPackagesGrouped.Select(g => g.OrderByDescending(x => x.Version).Skip(1)).SelectMany(s => s), searchToken), searchToken.SearchFilter.IncludePrerelease, cancellationToken);
            var remainingTransitiveItems = await GetMetadataFromIdentityForPackagesAndSortAsync(PerformLookup(latestTransitiveGrouped.Select(g => g.OrderByDescending(x => x.Version).Skip(1)).SelectMany(s => s), searchToken), searchToken.SearchFilter.IncludePrerelease, cancellationToken);

            // Combine the installed and transitive packages
            IPackageSearchMetadata[] items = installedItems
                .Concat(remainingInstalledItems)
                .Concat(transitiveItems)
                .Concat(remainingTransitiveItems)
                .ToArray();

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
    }
}
