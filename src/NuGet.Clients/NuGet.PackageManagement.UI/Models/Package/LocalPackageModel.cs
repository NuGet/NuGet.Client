// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    class LocalPackageModel : PackageModel, IVulnerable
    {
        private readonly IVulnerable _vulnerableCapability;

        public LocalPackageModel(PackageIdentity identity,
            string packagePath,
            IVulnerable vulnerableCapability,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null)
            : base(identity, title, description, authors, projectUrl, tags, copyright)
        {
            PackagePath = packagePath;
            _vulnerableCapability = vulnerableCapability;
        }

        public string PackagePath { get; }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo> Vulnerabilities => _vulnerableCapability.Vulnerabilities;

        public bool IsVulnerable => _vulnerableCapability.IsVulnerable;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity => _vulnerableCapability.VulnerabilityMaxSeverity;
    }
}
