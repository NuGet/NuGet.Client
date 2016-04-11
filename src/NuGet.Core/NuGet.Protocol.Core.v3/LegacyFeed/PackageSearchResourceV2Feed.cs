// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV2Feed : PackageSearchResource
    {
        private readonly HttpSource _httpSource;
        private readonly Configuration.PackageSource _packageSource;
        private readonly V2FeedParser _feedParser;

        public PackageSearchResourceV2Feed(HttpSourceResource httpSourceResource, string baseAddress, Configuration.PackageSource packageSource)
        {
            if (httpSourceResource == null)
            {
                throw new ArgumentNullException(nameof(httpSourceResource));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            _httpSource = httpSourceResource.HttpSource;
            _packageSource = packageSource;
            _feedParser = new V2FeedParser(_httpSource, baseAddress, packageSource);
        }

        public async override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            var query = await _feedParser.Search(
                searchTerm,
                filters,
                skip,
                take,
                log,
                cancellationToken);

            // NuGet.Server does not group packages by id, this resource needs to handle it.
            var results = query.GroupBy(p => p.Id)
                .Select(group => group.OrderByDescending(p => p.Version).First())
                .Select(package => CreatePackageSearchResult(package, filters, log, cancellationToken));

            return results.ToList();
        }

        private IPackageSearchMetadata CreatePackageSearchResult(
            V2FeedPackageInfo package,
            SearchFilter filter,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            var metadata = new PackageSearchMetadataV2Feed(package);
            return metadata
                .WithVersions(() => GetVersions(package, filter, log, cancellationToken));
        }

        public async Task<IEnumerable<VersionInfo>> GetVersions(
            V2FeedPackageInfo package,
            SearchFilter filter,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // apply the filters to the version list returned
            var packages = await _feedParser.FindPackagesByIdAsync(
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
                        PackageSearchMetadata = new PackageSearchMetadataV2Feed(versionPackage)
                    };

                    results.Add(versionInfo);
                }
            }
            return results;
        }
    }
}