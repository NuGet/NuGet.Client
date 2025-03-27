// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using ContractItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.UI.Models
{
    internal class PackageModelFactory
    {
        private readonly INuGetSearchService _searchService;
        private readonly INuGetPackageFileService _packageFileService;
        private readonly IPackageVulnerabilityService _packageVulnerabilityService;
        private readonly bool _includePrerelease;
        private IReadOnlyCollection<PackageSourceContextInfo> _packageSources;

        public PackageModelFactory(INuGetSearchService searchService, INuGetPackageFileService packageFileService, IPackageVulnerabilityService packageVulnerabilityService, bool includePrerelease, IReadOnlyCollection<PackageSourceContextInfo> packageSources)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(_searchService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(_packageFileService));
            _packageVulnerabilityService = packageVulnerabilityService ?? throw new ArgumentNullException(nameof(_packageVulnerabilityService));
            _includePrerelease = includePrerelease;
            _packageSources = packageSources ?? throw new ArgumentNullException(nameof(_packageSources));
        }

        public PackageModel Create(PackageSearchMetadataContextInfo metadata, ContractItemFilter itemFilter)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            EmbeddedResourcesCapability embeddedResources = new EmbeddedResourcesCapability(_packageFileService, metadata.Identity!, metadata.ReadmeUrl);

            if (metadata.PackagePath != null)
            {
                PackageMetadataRetrievalAdapter packageMetadataRetrievalAdapter = new PackageMetadataRetrievalAdapter(_searchService, metadata.Identity!, _packageSources, _includePrerelease);
                IVulnerableCapable vulnerableCapability = new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapter);

                if (itemFilter.Equals(ContractItemFilter.All))
                {
                    // Package from a local folder
                    return new LocalPackageModel(
                        metadata.Identity!,
                        metadata.PackagePath,
                        vulnerableCapability,
                        embeddedResources,
                        metadata.Title,
                        metadata.Description,
                        metadata.Authors,
                        metadata.ProjectUrl,
                        metadata.Tags?.Split(','),
                        metadata.OwnersList,
                        metadata.DependencySets,
                        metadata.Summary,
                        metadata.Published,
                        metadata.LicenseMetadata,
                        metadata.LicenseUrl,
                        metadata.RequireLicenseAcceptance,
                        metadata.IconUrl);
                }

                IDeprecationCapable deprecationCapable = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapter);

                // Installed package with a PackageReference
                return new ReferencedPackageModel(
                    metadata.Identity!,
                    metadata.PackagePath,
                    vulnerableCapability,
                    deprecationCapable,
                    embeddedResources,
                    metadata.Title,
                    metadata.Description,
                    metadata.Authors,
                    metadata.ProjectUrl,
                    metadata.Tags?.Split(','),
                    metadata.OwnersList,
                    metadata.DependencySets,
                    metadata.Summary,
                    metadata.Published,
                    metadata.LicenseMetadata,
                    metadata.LicenseUrl,
                    metadata.RequireLicenseAcceptance,
                    metadata.ReportAbuseUrl?.ToString(),
                    metadata.IconUrl);
            }
            else
            {
                // Transitive dependencies are only available in the Installed tab
                if (metadata.TransitiveOrigins != null)
                {
                    IVulnerableCapable vulnerableDatabaseCapability = new VulnerableDatabaseCapability(_packageVulnerabilityService, metadata.Identity!);
                    return new TransitivelyReferencedPackageModel(
                        metadata.Identity!,
                        vulnerableDatabaseCapability,
                        embeddedResources,
                        metadata.TransitiveOrigins,
                        metadata.Title,
                        metadata.Description,
                        metadata.Authors,
                        metadata.ProjectUrl,
                        metadata.Tags?.Split(','),
                        metadata.OwnersList,
                        metadata.DependencySets,
                        metadata.Summary,
                        metadata.Published,
                        metadata.LicenseMetadata,
                        metadata.LicenseUrl,
                        metadata.RequireLicenseAcceptance,
                        metadata.ReportAbuseUrl?.ToString(),
                        metadata.IconUrl);
                }

                PackageMetadataRetrievalAdapter packageMetadataRetrievalAdapter = new PackageMetadataRetrievalAdapter(_searchService, metadata.Identity!, _packageSources, _includePrerelease);
                IDeprecationCapable deprecationCapable = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapter);
                VulnerablePackageMetadataCapability vulnerableCapability = new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapter);

                if (metadata.IsRecommended)
                {
                    var recommenderVersion = metadata.RecommenderVersion ?? throw new ArgumentNullException(nameof(metadata.RecommenderVersion));

                    return new RecommendedPackageModel(
                        metadata.Identity!,
                        vulnerableCapability,
                        deprecationCapable,
                        embeddedResources,
                        recommenderVersion,
                        metadata.Title,
                        metadata.Description,
                        metadata.Authors,
                        metadata.ProjectUrl,
                        metadata.Tags?.Split(','),
                        metadata.OwnersList,
                        metadata.DependencySets,
                        metadata.Summary,
                        metadata.Published,
                        metadata.LicenseMetadata,
                        metadata.LicenseUrl,
                        metadata.RequireLicenseAcceptance,
                        metadata.IsListed,
                        metadata.PackageDetailsUrl,
                        metadata.DownloadCount,
                        metadata.ReadmeUrl,
                        metadata.IconUrl);
                }

                return new RemotePackageModel(
                    metadata.Identity!,
                    vulnerableCapability,
                    deprecationCapable,
                    embeddedResources,
                    metadata.Title,
                    metadata.Description,
                    metadata.Authors,
                    metadata.ProjectUrl,
                    metadata.Tags?.Split(','),
                    metadata.OwnersList,
                    metadata.DependencySets,
                    metadata.Summary,
                    metadata.Published,
                    metadata.LicenseMetadata,
                    metadata.LicenseUrl,
                    metadata.RequireLicenseAcceptance,
                    metadata.IsListed,
                    metadata.PackageDetailsUrl,
                    metadata.DownloadCount,
                    metadata.ReadmeUrl,
                    metadata.IconUrl);
            }
        }
    }
}
