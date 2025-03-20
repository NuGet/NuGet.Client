// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal class RemotePackageModel : PackageModel, IKnownOwnersCapable, IDeprecationCapable, IVulnerableCapable
    {
        private readonly IDeprecationCapable _deprecationCapability;
        private readonly IVulnerableCapable _vulnerableCapability;
        private readonly IKnownOwnersCapable _knownOwnersCapability;

        public RemotePackageModel(
            PackageIdentity identity,
            IVulnerableCapable vulnerableCapability,
            IDeprecationCapable deprecationCapability,
            IEmbeddedResources embeddedResources,
            IKnownOwnersCapable knownOwnersCapability,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null,
            IReadOnlyList<string>? ownersList = null,
            IReadOnlyCollection<PackageDependencyGroup>? packageDependencyGroups = null,
            string? summary = null,
            DateTimeOffset? publishedDate = null,
            LicenseMetadata? licenseMetadata = null,
            Uri? licenseUrl = null,
            bool requireLicenseAcceptance = false,
            bool isListed = false,
            Uri? packageDetailsUrl = null,
            long? downloadCount = null,
            Uri? readmeUrl = null)
            : base(identity, embeddedResources, title, description, authors, projectUrl, tags, copyright, ownersList, packageDependencyGroups, summary, publishedDate, licenseMetadata, licenseUrl, requireLicenseAcceptance)
        {
            IsListed = isListed;
            PackageDetailsUrl = packageDetailsUrl;
            DownloadCount = downloadCount;
            _deprecationCapability = deprecationCapability ?? throw new ArgumentNullException(nameof(deprecationCapability));
            _knownOwnersCapability = knownOwnersCapability ?? throw new ArgumentNullException(nameof(knownOwnersCapability));
            _vulnerableCapability = vulnerableCapability ?? throw new ArgumentNullException(nameof(vulnerableCapability));
            ReadmeUrl = readmeUrl;
        }

        public bool IsListed { get; }
        public Uri? PackageDetailsUrl { get; }
        public long? DownloadCount { get; }
        public Uri? ReadmeUrl { get; }
        public IReadOnlyList<KnownOwner>? KnownOwners => _knownOwnersCapability?.KnownOwners;

        public bool IsDeprecated => _deprecationCapability.IsDeprecated;

        public PackageDeprecationReasonEnum PackageDeprecationReasons => _deprecationCapability.PackageDeprecationReasons;

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo>? Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;

        public async Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            await _vulnerableCapability.PopulateDataAsync(cancellationToken);
            await _deprecationCapability.PopulateDataAsync(cancellationToken);
        }
    }
}
