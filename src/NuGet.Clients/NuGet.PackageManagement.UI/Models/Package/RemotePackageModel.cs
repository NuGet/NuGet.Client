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
    class RemotePackageModel : PackageModel, IVulnerable
    {
        private readonly IVulnerable _vulnerableCapability;

        public RemotePackageModel(
            PackageIdentity identity,
            IVulnerable vulnerableCapability,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null,
            bool isListed = false,
            DateTimeOffset? dateTimeOffset = null,
            Uri? packageDetailsUrl = null,
            long? downloadCount = null,
            IEnumerable<PackageDependencyGroup>? dependencySets = null)
            : base(identity, title, description, authors, projectUrl, tags, copyright)
        {
            IsListed = isListed;
            Published = dateTimeOffset;
            PackageDetailsUrl = packageDetailsUrl;
            DownloadCount = downloadCount;
            DependencySets = dependencySets;
            _vulnerableCapability = vulnerableCapability;
        }

        public bool IsListed { get; }
        public DateTimeOffset? Published { get; }
        public Uri? PackageDetailsUrl { get; }
        public long? DownloadCount { get; }
        public IEnumerable<PackageDependencyGroup>? DependencySets { get; }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo> Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;
    }
}
