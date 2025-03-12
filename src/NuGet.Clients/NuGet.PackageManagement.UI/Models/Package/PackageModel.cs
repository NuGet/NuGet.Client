// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public abstract class PackageModel
    {
        protected PackageModel(PackageIdentity identity,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null,
            IReadOnlyList<string>? ownersList = null,
            IReadOnlyCollection<PackageDependencyGroup>? packageDependencyGroups = null,
            string? summary = null)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Title = title;
            Description = description;
            Authors = authors;
            ProjectUrl = projectUrl;
            Tags = tags;
            Copyright = copyright;
            OwnersList = ownersList;
            Summary = summary;

            if (packageDependencyGroups != null && packageDependencyGroups.Count > 0)
            {
                DependencySets = packageDependencyGroups.Select(e => new PackageDependencySetMetadata(e)).ToArray();
            }
        }

        public PackageIdentity Identity { get; }

        public string Id => Identity.Id;

        public NuGetVersion Version => Identity.Version;

        public string? Title { get; }

        public string? Description { get; }

        public string? Authors { get; }

        public IReadOnlyList<string>? OwnersList { get; }

        public IReadOnlyCollection<PackageDependencySetMetadata>? DependencySets { get; }

        public Uri? ProjectUrl { get; }

        public string[]? Tags { get; }

        public string? Summary { get; }

        public string? Copyright { get; }
    }
}
