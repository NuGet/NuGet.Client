// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Model;

namespace NuGet.PackageManagement.UI.Models
{
    public class ReferencedPackageModel : PackageModel, IDeprecationCapable
    {
        private readonly IDeprecationCapable _deprecationCapability;

        public ReferencedPackageModel(
            PackageIdentity identity,
            string packagePath,
            IVulnerableCapable vulnerableCapability,
            IDeprecationCapable deprecationCapability,
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
                  vulnerableCapability,
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
            _deprecationCapability = deprecationCapability;
            PackagePath = packagePath;
        }

        public string PackagePath { get; }

        public string? ReportAbuseUrl { get; }

        public bool IsDeprecated => _deprecationCapability.IsDeprecated;

        public PackageDeprecationReasonEnum PackageDeprecationReasons => _deprecationCapability.PackageDeprecationReasons;

        public override async Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            await base.PopulateDataAsync(cancellationToken);
            await _deprecationCapability.PopulateDataAsync(cancellationToken);
        }
    }
}
