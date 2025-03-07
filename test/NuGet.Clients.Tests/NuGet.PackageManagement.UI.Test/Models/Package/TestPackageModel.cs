// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI.Test
{
    internal class TestPackageModel : PackageModel
    {
        public TestPackageModel(PackageIdentity identity,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null)
            : base(identity, title, description, authors, projectUrl, tags, copyright)
        {
        }
    }

    internal class TestRemotePackageModel : RemotePackageModel
    {
        public TestRemotePackageModel(PackageIdentity identity,
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
            : base(identity, vulnerableCapability, title, description, authors, projectUrl, tags, copyright, isListed, dateTimeOffset, packageDetailsUrl, downloadCount, dependencySets)
        {
        }
    }

    internal class TestLocalPackageModel : LocalPackageModel
    {
        public TestLocalPackageModel(PackageIdentity identity,
            string packagePath,
            IVulnerable vulnerableCapability,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null)
            : base(identity, packagePath, vulnerableCapability, title, description, authors, projectUrl, tags, copyright)
        {
        }
    }
}
