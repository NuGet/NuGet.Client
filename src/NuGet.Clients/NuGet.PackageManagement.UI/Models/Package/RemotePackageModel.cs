// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class RemotePackageModel : PackageModel, IVulnerable, IKnownOwnersCapable
    {
        private readonly IVulnerable _vulnerableCapability;
        private readonly IKnownOwnersCapable _knownOwnersCapability;

        public RemotePackageModel(
            PackageIdentity identity,
            IVulnerable vulnerableCapability,
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
            _vulnerableCapability = vulnerableCapability;
            _knownOwnersCapability = knownOwnersCapability;
            ReadmeUrl = readmeUrl;
        }

        public bool IsListed { get; }
        public Uri? PackageDetailsUrl { get; }
        public long? DownloadCount { get; }
        public Uri? ReadmeUrl { get; }
        public IReadOnlyList<KnownOwner>? KnownOwners => _knownOwnersCapability?.KnownOwners;

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo> Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;
    }
}
