// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Common;
using NuGet.Packaging;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Helper class allowing creation/alteration of immutable package metadata objects.
    /// </summary>
    public class PackageSearchMetadataBuilder
    {
        private readonly IPackageSearchMetadata _metadata;
        private AsyncLazy<IEnumerable<VersionInfo>> _lazyVersionsFactory;

        public class ClonedPackageSearchMetadata : IPackageSearchMetadata
        {
            public string Authors { get; set; }
            public IEnumerable<PackageDependencyGroup> DependencySets { get; set; }
            public string Description { get; set; }
            public long? DownloadCount { get; set; }
            public Uri IconUrl { get; set; }
            public PackageIdentity Identity { get; set; }
            public Uri LicenseUrl { get; set; }
            public string Owners { get; set; }
            public Uri ProjectUrl { get; set; }
            public DateTimeOffset? Published { get; set; }
            public Uri ReportAbuseUrl { get; set; }
            public Uri PackageDetailsUrl { get; set; }
            public bool RequireLicenseAcceptance { get; set; }
            public string Summary { get; set; }
            public string Tags { get; set; }
            public string Title { get; set; }
            public bool PrefixReserved { get; set; }
            public LicenseMetadata LicenseMetadata { get; set; }
            public PackageDeprecationMetadata DeprecationMetadata { get; set; }

            public AsyncLazy<IEnumerable<VersionInfo>> LazyVersionsFactory { get; set; }

            public async Task<IEnumerable<VersionInfo>> GetVersionsAsync() => await LazyVersionsFactory;

            public bool IsListed { get; set; }
        }

        private PackageSearchMetadataBuilder(IPackageSearchMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            _metadata = metadata;
        }

        public PackageSearchMetadataBuilder WithVersions(AsyncLazy<IEnumerable<VersionInfo>> lazyVersionsFactory)
        {
            _lazyVersionsFactory= lazyVersionsFactory;
            return this;
        }

        public IPackageSearchMetadata Build()
        {
            var clonedMetadata = new ClonedPackageSearchMetadata
            {
                Authors = _metadata.Authors,
                DependencySets = _metadata.DependencySets ?? Enumerable.Empty<PackageDependencyGroup>(),
                Description = _metadata.Description,
                DownloadCount = _metadata.DownloadCount,
                IconUrl = _metadata.IconUrl,
                Identity = _metadata.Identity,
                LicenseUrl = _metadata.LicenseUrl,
                Owners = _metadata.Owners,
                ProjectUrl = _metadata.ProjectUrl,
                Published = _metadata.Published,
                ReportAbuseUrl = _metadata.ReportAbuseUrl,
                PackageDetailsUrl = _metadata.PackageDetailsUrl,
                RequireLicenseAcceptance = _metadata.RequireLicenseAcceptance,
                Summary = _metadata.Summary,
                Tags = _metadata.Tags,
                Title = _metadata.Title,
                LazyVersionsFactory = _lazyVersionsFactory,
                IsListed = _metadata.IsListed,
                PrefixReserved = _metadata.PrefixReserved,
                LicenseMetadata = _metadata.LicenseMetadata,
                DeprecationMetadata = _metadata.DeprecationMetadata
            };

            return clonedMetadata;
        }

        public static PackageSearchMetadataBuilder FromMetadata(IPackageSearchMetadata metadata) 
            => new PackageSearchMetadataBuilder(metadata);

        public static PackageSearchMetadataBuilder FromIdentity(PackageIdentity identity)
        {
            var metadata = new ClonedPackageSearchMetadata
            {
                Identity = identity,
                Title = identity.Id,
                Summary = string.Empty,
                Authors = string.Empty
            };
            return FromMetadata(metadata);
        }
    }

    /// <summary>
    /// Shortcut methods to create altered metadata objects with new versions.
    /// </summary>
    public static class PackageSearchMetadataExtensions
    {
        public static IPackageSearchMetadata WithVersions(this IPackageSearchMetadata metadata, IEnumerable<VersionInfo> versions)
        {
            return PackageSearchMetadataBuilder
                .FromMetadata(metadata)
                .WithVersions(AsyncLazy.New(versions))
                .Build();
        }

        public static IPackageSearchMetadata WithVersions(this IPackageSearchMetadata metadata, Func<Task<IEnumerable<VersionInfo>>> asyncValueFactory)
        {
            return PackageSearchMetadataBuilder
                .FromMetadata(metadata)
                .WithVersions(AsyncLazy.New(asyncValueFactory))
                .Build();
        }

        public static IPackageSearchMetadata WithVersions(this IPackageSearchMetadata metadata, Func<IEnumerable<VersionInfo>> valueFactory)
        {
            return PackageSearchMetadataBuilder
                .FromMetadata(metadata)
                .WithVersions(AsyncLazy.New(valueFactory))
                .Build();
        }
    }
}
