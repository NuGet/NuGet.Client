// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public abstract class PackageModel : IEmbeddedResources
    {
        private IEmbeddedResources _embeddedResources;

        internal PackageModel(PackageIdentity identity,
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
        {
            _embeddedResources = embeddedResources ?? throw new ArgumentNullException(nameof(embeddedResources));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Title = title;
            Description = description;
            Authors = authors;
            ProjectUrl = projectUrl;
            Tags = tags;
            Copyright = copyright;
            OwnersList = ownersList;
            Summary = summary;
            PublishedDate = publishedDate;
            LicenseMetadata = licenseMetadata;
            LicenseUrl = licenseUrl;
            RequireLicenseAcceptance = requireLicenseAcceptance;

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

        public LicenseMetadata? LicenseMetadata { get; }

        public Uri? LicenseUrl { get; }

        public bool RequireLicenseAcceptance { get; }

        public DateTimeOffset? PublishedDate { get; }

        public Uri? ReadmeUri => _embeddedResources.ReadmeUri;

        public ValueTask<Stream?> GetIconAsync(CancellationToken cancellationToken) => _embeddedResources.GetIconAsync(cancellationToken);

        public ValueTask<Stream?> GetLicenseAsync(CancellationToken cancellationToken) => _embeddedResources.GetLicenseAsync(cancellationToken);

        public ValueTask<Stream?> GetReadmeAsync(CancellationToken cancellationToken) => _embeddedResources.GetReadmeAsync(cancellationToken);
    }
}
