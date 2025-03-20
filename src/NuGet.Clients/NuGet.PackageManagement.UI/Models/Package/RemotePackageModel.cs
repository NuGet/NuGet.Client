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
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class RemotePackageModel : PackageModel, IKnownOwnersCapable, IDeprecationCapable
    {
        private readonly IDeprecationCapable _deprecationCapability;
        private readonly IKnownOwnersCapable _knownOwnersCapability;

        public RemotePackageModel(
            PackageIdentity identity,
            IVulnerableCapable vulnerableCapability,
            IDeprecationCapable deprecationCapability,
            IEmbeddedResources embeddedResources,
            IKnownOwnersCapable knownOwnersCapability,
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
            : base(identity, embeddedResources, vulnerableCapability, title, description, authors, projectUrl, tags, copyright, ownersList, packageDependencyGroups, summary, publishedDate, licenseMetadata, licenseUrl, requireLicenseAcceptance)
        {
            IsListed = isListed;
            PackageDetailsUrl = packageDetailsUrl;
            DownloadCount = downloadCount;
            _deprecationCapability = deprecationCapability;
            _knownOwnersCapability = knownOwnersCapability;
            ReadmeUrl = readmeUrl;
        }

        public bool IsListed { get; }
        public Uri? PackageDetailsUrl { get; }
        public long? DownloadCount { get; }
        public Uri? ReadmeUrl { get; }
        public IReadOnlyList<KnownOwner>? KnownOwners => _knownOwnersCapability?.KnownOwners;

        public bool IsDeprecated => _deprecationCapability.IsDeprecated;

        public PackageDeprecationReasonEnum PackageDeprecationReasons => _deprecationCapability.PackageDeprecationReasons;

        public override async Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            await base.PopulateDataAsync(cancellationToken);
            await _deprecationCapability.PopulateDataAsync(cancellationToken);
        }
    }
}
