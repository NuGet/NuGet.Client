// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI.Models
{
    internal class InstalledPackageModel : LocalPackageModel
    {
        public InstalledPackageModel(
            PackageIdentity identity,
            string packagePath,
            IVulnerable vulnerabilityCapability,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null,
            IReadOnlyList<string>? ownersList = null,
            IReadOnlyCollection<PackageDependencyGroup>? packageDependencyGroups = null,
            string? summary = null,
            DateTimeOffset? published = null,
            string? reportAbuseUrl = null)
            : base(identity,
                  packagePath,
                  vulnerabilityCapability,
                  title,
                  description,
                  authors,
                  projectUrl,
                  tags,
                  copyright,
                  ownersList,
                  packageDependencyGroups,
                  summary,
                  published)
        {
            ReportAbuseUrl = reportAbuseUrl;
        }

        public string? ReportAbuseUrl { get; }
    }
}
