// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.VisualStudio
{
    public class PackageSearchResourceV3 : PackageSearchResource
    {
        private readonly RawSearchResourceV3 _rawSearchResource;
        private readonly PackageMetadataResource _metadataResource;

        public PackageSearchResourceV3(RawSearchResourceV3 searchResource, PackageMetadataResource metadataResource)
            : base()
        {
            _rawSearchResource = searchResource;
            _metadataResource = metadataResource;
        }

        public async override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take, CancellationToken cancellationToken)
        {
            var searchResultJsonObjects = await _rawSearchResource.Search(searchTerm, filter, skip, take, Logging.NullLogger.Instance, cancellationToken);

            var searchResults = searchResultJsonObjects
                .Select(s => s.FromJToken<PackageSearchMetadata>())
                .Select(m => m.WithVersions(() => GetVersions(m, filter)))
                .ToArray();

            return searchResults;
        }

        private static IEnumerable<VersionInfo> GetVersions(PackageSearchMetadata metadata, SearchFilter filter)
        {
            var versions = metadata.OnDemandParsedVersions.Value;

            // TODO: in v2, we only have download count for all versions, not per version.
            // To be consistent, in v3, we also use total download count for now.
            var totalDownloadCount = versions.Select(v => v.DownloadCount).Sum();
            versions = versions
                .Select(v => v.Version)
                .Where(v => filter.IncludePrerelease || !v.IsPrerelease)
                .Concat(new[] { metadata.Version })
                .Distinct()
                .Select(v => new VersionInfo(v, totalDownloadCount))
                .ToArray();

            return versions;
        }
    }
}