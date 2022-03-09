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
            var searchToken = ThrowIfNotFeedSearchContinuationToken(continuationToken);

            // Remove transitive packages from project references
            IEnumerable<PackageCollectionItem> pkgsWithOrigins = _transitivePackages
                .Where(t => t.PackageReferences.Any(x => x is ITransitivePackageReferenceContextInfo y && y.TransitiveOrigins.Any()));

            IPackageSearchMetadata[] intalledItems = await DoSearchAsync(_installedPackages, searchToken, cancellationToken);
            IPackageSearchMetadata[] transitiveItems = await DoSearchAsync(pkgsWithOrigins, searchToken, cancellationToken);
            IPackageSearchMetadata[] searchItems = intalledItems.Concat(transitiveItems).ToArray();

            return CreateResult(searchItems);
        }

        internal override async Task<IPackageSearchMetadata> GetPackageMetadataAsync<T>(T pkgIdentity, bool includePrerelease, CancellationToken cancellationToken)
        {
            if (pkgIdentity is PackageCollectionItem identity)
            {
                IEnumerable<ITransitivePackageReferenceContextInfo> transitivePRs = identity.PackageReferences.OfType<ITransitivePackageReferenceContextInfo>();

                ITransitivePackageReferenceContextInfo transitivePR = transitivePRs.OrderByDescending(x => x.Identity.Version).FirstOrDefault();

                if (transitivePR != null)
                {
                    IReadOnlyCollection<PackageIdentity> transitiveOrigins = transitivePR.TransitiveOrigins?.Select(to => to.Identity).ToArray() ?? Array.Empty<PackageIdentity>();

                    if (transitiveOrigins.Any())
                    {
                        // Get only local metadata. We don't want Deprecation and Vulnerabilities Metadata on Transitive packages
                        IPackageSearchMetadata packageMetadata = await _metadataProvider.GetOnlyLocalPackageMetadataAsync(identity, cancellationToken);

                        if (packageMetadata == null) // Edge case: local metadata not found
                        {
                            packageMetadata = PackageSearchMetadataBuilder.FromIdentity(identity).Build(); // create metadata object only with ID
                        }

                        var ts = new TransitivePackageSearchMetadata(packageMetadata, transitiveOrigins);
                        return ts;
                    }
                    else
                    {
                        return await base.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
                    }
                }
                else
                {
                    return await base.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
                }
            }
            else // pkgIdentity is PackageIdentity
            {
                return await base.GetPackageMetadataAsync(pkgIdentity, includePrerelease, cancellationToken);
            }
        }
    }
}
