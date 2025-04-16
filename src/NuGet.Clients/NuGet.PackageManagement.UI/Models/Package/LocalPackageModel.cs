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
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.Models.Package
{
    internal class LocalPackageModel : PackageModel, IVulnerableCapable
    {
        private IVulnerableCapable _vulnerableCapability;

        public LocalPackageModel(PackageIdentity identity,
            string packagePath,
            IVulnerableCapable vulnerableCapability,
            IEmbeddedResourcesCapable embeddedResources,
            string? title,
            string? description,
            string? authors,
            Uri? projectUrl,
            string[]? tags,
            IReadOnlyList<string>? ownersList,
            IReadOnlyCollection<PackageDependencyGroup>? packageDependencyGroups,
            string? summary,
            DateTimeOffset? publishedDate,
            LicenseMetadata? licenseMetadata,
            Uri? licenseUrl,
            bool requireLicenseAcceptance,
            Uri? iconUrl)
            : base(identity, embeddedResources, title, description, authors, projectUrl, tags, ownersList, packageDependencyGroups, summary, publishedDate, licenseMetadata, licenseUrl, requireLicenseAcceptance, iconUrl)
        {
            _vulnerableCapability = vulnerableCapability ?? throw new ArgumentNullException(nameof(vulnerableCapability));
            PackagePath = packagePath;
        }

        public string PackagePath { get; }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo>? Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;

        public override async Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            await _vulnerableCapability.PopulateDataAsync(cancellationToken);
        }
    }
}
