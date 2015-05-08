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

namespace NuGet.Protocol.VisualStudio
{
    public class UISearchResourceV2 : UISearchResource
    {
        private readonly IPackageRepository V2Client;

        public UISearchResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public UISearchResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public override async Task<IEnumerable<UISearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            return await GetSearchResultsForVisualStudioUI(searchTerm, filters, skip, take, cancellationToken);
        }

        private async Task<IEnumerable<UISearchMetadata>> GetSearchResultsForVisualStudioUI(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
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
                    query = query.OrderByDescending(p => p.DownloadCount)
                        .ThenBy(p => p.Id);

                    // Some V2 sources, e.g. NuGet.Server, local repository, the result contains all 
                    // versions of each package. So we need to group the result by Id.
                    var collapsedQuery = query.AsEnumerable().AsCollapsed();

                    // execute the query
                    var allPackages = collapsedQuery
                        .Skip(skip)
                        .Take(take)
                        .ToList();

                    // fetch version info in parallel
                    var tasks = new Queue<Task<UISearchMetadata>>();
                    foreach (var p in allPackages)
                    {
                        tasks.Enqueue(CreatePackageSearchResult(p, filters, cancellationToken));
                    }

                    var results = new List<UISearchMetadata>();
                    while (tasks.Count > 0)
                    {
                        var metadata = await tasks.Dequeue();
                        results.Add(metadata);
                    }

                    return results;
                });
        }

        private async Task<UISearchMetadata> CreatePackageSearchResult(IPackage package, SearchFilter filters, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // apply the filters to the version list returned
                    var versions = V2Client.FindPackagesById(package.Id)
                        .Where(p => filters.IncludeDelisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                        .Where(v => filters.IncludePrerelease || String.IsNullOrEmpty(v.Version.SpecialVersion)).ToArray();

                    if (!versions.Any())
                    {
                        versions = new[] { package };
                    }

                    var id = package.Id;
                    var version = V2Utilities.SafeToNuGetVer(package.Version);
                    var title = package.Title;
                    var summary = package.Summary;

                    var nuGetVersions = versions.Select(p =>
                        new VersionInfo(V2Utilities.SafeToNuGetVer(p.Version), p.DownloadCount));

                    if (String.IsNullOrWhiteSpace(summary))
                    {
                        summary = package.Description;
                    }

                    if (String.IsNullOrEmpty(title))
                    {
                        title = id;
                    }

                    var iconUrl = package.IconUrl;
                    var identity = new PackageIdentity(id, version);
                    var searchMetaData = new UISearchMetadata(identity, title, summary, iconUrl, nuGetVersions, UIMetadataResourceV2.GetVisualStudioUIPackageMetadata(package));
                    return searchMetaData;
                });
        }
    }
}
