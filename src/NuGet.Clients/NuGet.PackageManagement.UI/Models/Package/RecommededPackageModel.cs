// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI.Models
{
    public class RecommendedPackageModel : RemotePackageModel
    {
        public RecommendedPackageModel(
            PackageIdentity identity,
            IVulnerableCapable vulnerableCapability,
            IEmbeddedResources embeddedResources,
            (string modelVersion, string vsixVersion) recommenderVersion,
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
                : base(identity, vulnerableCapability, embeddedResources, title, description, authors, projectUrl, tags, ownersList, packageDependencyGroups, summary, publishedDate, licenseMetadata, licenseUrl, requireLicenseAcceptance, isListed, packageDetailsUrl, downloadCount, readmeUrl)
        {
            RecommenderVersion = recommenderVersion;
        }

        public (string modelVersion, string vsixVersion) RecommenderVersion { get; }
    }
}

