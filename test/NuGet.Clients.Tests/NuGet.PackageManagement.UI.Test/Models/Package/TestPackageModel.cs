// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    internal class TestPackageModel : PackageModel
    {
        public TestPackageModel(PackageIdentity identity,
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
            bool requireLicenseAcceptance = false)
            : base(identity, embeddedResources, title, description, authors, projectUrl, tags, copyright, ownersList, packageDependencyGroups, summary, publishedDate, licenseMetadata, licenseUrl, requireLicenseAcceptance)
        {
        }
    }
}
