// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV3 : PackageSearchResource
    {
        private readonly RawSearchResourceV3 _rawSearchResource;

        public PackageSearchResourceV3(RawSearchResourceV3 searchResource)
            : base()
        {
            _rawSearchResource = searchResource;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            try
            {
                var searchResultJsonObjects = (await _rawSearchResource.Search(searchTerm, filter, skip, take, Common.NullLogger.Instance, cancellationToken)).ToList();

                // Some nuget server not honoring our skip parameter nor take parameter, just returning everything they have.
                // Then it's more than we asked, it bogs down whole processing with thousands of items. Still we need to let user see things in paginated way.
                if (searchResultJsonObjects?.Count > take)
                {
                    if (searchResultJsonObjects?.Count >= skip + take)
                    {
                        searchResultJsonObjects = searchResultJsonObjects.Skip(skip).Take(take).ToList();
                    }
                    else
                    {
                        searchResultJsonObjects = searchResultJsonObjects.Take(take).ToList();
                    }                    
                }

                var metadataCache = new MetadataReferenceCache();

                var searchResults = searchResultJsonObjects
                    .Select(s => s.FromJToken<PackageSearchMetadata>())
                    .Select(m => m.WithVersions(() => GetVersions(m, filter)))
                    .Select(m => metadataCache.GetObject((PackageSearchMetadataBuilder.ClonedPackageSearchMetadata)m))
                    .ToArray();
                return searchResults;
            }
            catch(Exception ex)
            {
                System.Console.WriteLine(ex);
                throw;
            }

           // return Enumerable.Empty<IPackageSearchMetadata>();
            
        }

        private static IEnumerable<VersionInfo> GetVersions(PackageSearchMetadata metadata, SearchFilter filter)
        {
            var versions = metadata.ParsedVersions;

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
