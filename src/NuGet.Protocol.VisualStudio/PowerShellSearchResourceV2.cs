// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PowerShellSearchResourceV2 : PSSearchResource
    {
        private IPackageRepository V2Client { get; }

        public PowerShellSearchResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override async Task<IEnumerable<PSSearchMetadata>> Search(string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            return await GetSearchResultsAsync(searchTerm, filters, skip, take, cancellationToken);
        }

        private async Task<IEnumerable<PSSearchMetadata>> GetSearchResultsAsync(string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var query = V2Client.Search(
                    searchTerm,
                    filters.SupportedFrameworks,
                    filters.IncludePrerelease);

                // V2 sometimes requires that we also use an OData filter for 
                // latest /latest prerelease version
                if (filters.IncludePrerelease)
                {
                    query = query.Where(p => p.IsAbsoluteLatestVersion);
                }
                else
                {
                    query = query.Where(p => p.IsLatestVersion);
                }

                query = query
                    .OrderByDescending(p => p.DownloadCount)
                    .ThenBy(p => p.Id);

                // Some V2 sources, e.g. NuGet.Server, local repository, the result contains all 
                // versions of each package. So we need to group the result by Id.
                var collapsedQuery = query.AsEnumerable().AsCollapsed();

                // execute the query
                var allPackages = collapsedQuery
                    .Skip(skip)
                    .Take(take)
                    .ToArray();

                return allPackages
                    .Select(p => CreatePackageSearchResult(p, filters, cancellationToken))
                    .ToArray();
            });
        }

        private PSSearchMetadata CreatePackageSearchResult(IPackage package,
                                                           SearchFilter filters,
                                                           CancellationToken cancellationToken)
        {
            var id = package.Id;
            var version = V2Utilities.SafeToNuGetVer(package.Version);
            var summary = package.Summary;

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = package.Description;
            }

            var iconUrl = package.IconUrl;
            var identity = new PackageIdentity(id, version);

            var versions = new Lazy<Task<IEnumerable<NuGetVersion>>>(() =>
                    GetVersionInfoAsync(package, filters, cancellationToken));

            var searchMetaData = new PSSearchMetadata(identity, versions, summary);

            return searchMetaData;
        }

        public Task<IEnumerable<NuGetVersion>> GetVersionInfoAsync(IPackage package,
            SearchFilter filters,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // apply the filters to the version list returned
                var versions = V2Client.FindPackagesById(package.Id)
                    .Where(p => filters.IncludeDelisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                    .Where(v => filters.IncludePrerelease || string.IsNullOrEmpty(v.Version.SpecialVersion)).ToArray();

                if (!versions.Any())
                {
                    versions = new[] { package };
                }

                var nuGetVersions = versions.Select(p => V2Utilities.SafeToNuGetVer(p.Version));

                return nuGetVersions;
            });
        }
    }
}
