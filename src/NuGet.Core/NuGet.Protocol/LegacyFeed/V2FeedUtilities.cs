// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class V2FeedUtilities
    {
        public static IPackageSearchMetadata CreatePackageSearchResult(
          V2FeedPackageInfo package,
          MetadataReferenceCache metadataCache,
          SearchFilter filter,
          V2FeedParser feedParser,
          Common.ILogger log,
          CancellationToken cancellationToken)
        {
            var metadata = new PackageSearchMetadataV2Feed(package, metadataCache);
            return metadata
                .WithVersions(() => GetVersions(package, metadataCache, filter, feedParser, log, cancellationToken));
        }

        private static async Task<IEnumerable<VersionInfo>> GetVersions(
            V2FeedPackageInfo package,
            MetadataReferenceCache metadataCache,
            SearchFilter filter,
            V2FeedParser feedParser,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // apply the filters to the version list returned
            var packages = await feedParser.FindPackagesByIdAsync(
                package.Id,
                filter.IncludeDelisted,
                filter.IncludePrerelease,
                log,
                cancellationToken);

            var uniqueVersions = new HashSet<NuGetVersion>();
            var results = new List<VersionInfo>();
            
            foreach (var versionPackage in packages.OrderByDescending(p => p.Version))
            {
                if (uniqueVersions.Add(versionPackage.Version))
                {
                    var versionInfo = new VersionInfo(versionPackage.Version, versionPackage.DownloadCount)
                    {
                        PackageSearchMetadata = new PackageSearchMetadataV2Feed(versionPackage, metadataCache)
                    };

                    results.Add(versionInfo);
                }
            }
            return results;
        }
    }
}