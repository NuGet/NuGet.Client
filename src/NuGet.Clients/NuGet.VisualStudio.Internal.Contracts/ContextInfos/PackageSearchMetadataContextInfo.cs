// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using MessagePack;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.VisualStudio.Internal.Contracts
{
    [MessagePackObject(keyAsPropertyName: true)]
    public sealed class PackageSearchMetadataContextInfo
    {
        public PackageIdentity? Identity { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Authors { get; set; }
        public Uri? IconUrl { get; set; }
        public string? Tags { get; set; }
        public Uri? LicenseUrl { get; set; }
        public string? Owners { get; set; }
        public Uri? ProjectUrl { get; set; }
        public DateTimeOffset? Published { get; set; }
        public Uri? ReportAbuseUrl { get; set; }
        public Uri? PackageDetailsUrl { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string? Summary { get; set; }
        public bool PrefixReserved { get; set; }
        public bool IsRecommended { get; set; }
        public (string modelVersion, string vsixVersion)? RecommenderVersion { get; set; }
        public bool IsListed { get; set; }
        public long? DownloadCount { get; set; }
        public IEnumerable<PackageDependencyGroup>? DependencySets { get; set; }
        [IgnoreMember]
        public LicenseMetadata? LicenseMetadata { get; set; }
        [IgnoreMember]
        public PackageReaderBase? PackageReader { get; set; }

        public static PackageSearchMetadataContextInfo Create(IPackageSearchMetadata packageSearchMetadata)
        {
            return new PackageSearchMetadataContextInfo()
            {
                Title = packageSearchMetadata.Title,
                Description = packageSearchMetadata.Description,
                Authors = packageSearchMetadata.Authors,
                IconUrl = packageSearchMetadata.IconUrl,
                Tags = packageSearchMetadata.Tags,
                Identity = packageSearchMetadata.Identity,
                LicenseUrl = packageSearchMetadata.LicenseUrl,
                Owners = packageSearchMetadata.Owners,
                ProjectUrl = packageSearchMetadata.ProjectUrl,
                Published = packageSearchMetadata.Published,
                ReportAbuseUrl = packageSearchMetadata.ReportAbuseUrl,
                PackageDetailsUrl = packageSearchMetadata.PackageDetailsUrl,
                RequireLicenseAcceptance = packageSearchMetadata.RequireLicenseAcceptance,
                Summary = packageSearchMetadata.Summary,
                PrefixReserved = packageSearchMetadata.PrefixReserved,
                IsListed = packageSearchMetadata.IsListed,
                DependencySets = packageSearchMetadata.DependencySets,
                DownloadCount = packageSearchMetadata.DownloadCount,
            };
        }
    }
}
