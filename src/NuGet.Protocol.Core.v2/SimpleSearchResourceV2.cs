// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class SimpleSearchResourceV2 : SimpleSearchResource
    {
        private readonly IPackageRepository V2Client;

        public SimpleSearchResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public SimpleSearchResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            var query = V2Client.Search(
                searchTerm,
                filters.SupportedFrameworks,
                filters.IncludePrerelease);

            // V2 sometimes requires that we also use an OData filter for latest/latest prerelease version
            if (filters.IncludePrerelease)
            {
                query = query.Where(p => p.IsAbsoluteLatestVersion);
            }
            else
            {
                query = query.Where(p => p.IsLatestVersion);
            }

            if (V2Client is LocalPackageRepository)
            {
                // if the repository is a local repo, then query contains all versions of packages.
                // we need to explicitly select the latest version.
                query = query.OrderBy(p => p.Id)
                    .ThenByDescending(p => p.Version)
                    .GroupBy(p => p.Id)
                    .Select(g => g.First());
            }

            // Now apply skip and take and the rest of the party
            var result = (IEnumerable<SimpleSearchMetadata>)query
                .Skip(skip)
                .Take(take)
                .ToList()
                .AsParallel()
                .AsOrdered()
                .Select(p => CreatePackageSearchResult(p))
                .ToList();

            return Task.FromResult(result);
        }

        private SimpleSearchMetadata CreatePackageSearchResult(IPackage package)
        {
            var versions = V2Client.FindPackagesById(package.Id);
            if (!versions.Any())
            {
                versions = new[] { package };
            }
            var id = package.Id;
            var version = V2Utilities.SafeToNuGetVer(package.Version);
            var summary = package.Summary;
            var nuGetVersions = versions.Select(p => V2Utilities.SafeToNuGetVer(p.Version));
            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = package.Description;
            }

            var iconUrl = package.IconUrl;
            var identity = new PackageIdentity(id, version);
            var searchMetaData = new SimpleSearchMetadata(identity, summary, nuGetVersions);
            return searchMetaData;
        }
    }
}
