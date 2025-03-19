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

namespace NuGet.PackageManagement.UI.Models
{
    public class ReferencedPackageModel : PackageModel, IVulnerableCapable
    {
        private readonly IVulnerableCapable _vulnerableCapability;

        public ReferencedPackageModel(
            PackageIdentity identity,
            string packagePath,
            IVulnerableCapable vulnerabilityCapability,
            IEmbeddedResources embeddedResources,
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
            string? reportAbuseUrl = null)
            : base(identity,
                  embeddedResources,
                  title,
                  description,
                  authors,
                  projectUrl,
                  tags,
                  copyright,
                  ownersList,
                  packageDependencyGroups,
                  summary,
                  publishedDate,
                  licenseMetadata,
                  licenseUrl,
                  requireLicenseAcceptance)
        {
            ReportAbuseUrl = reportAbuseUrl;
            _vulnerableCapability = vulnerabilityCapability;
            PackagePath = packagePath;
        }

        public string PackagePath { get; }

        public string? ReportAbuseUrl { get; }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo>? Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;

        public Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            return _vulnerableCapability.PopulateDataAsync(cancellationToken);
        }
    }
}
