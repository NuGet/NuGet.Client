using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Client.VisualStudio;
using NuGet.Versioning;
using NuGet.PackagingCore;

namespace NuGet.Client.V2.VisualStudio
{
    public class V2UISearchResource : UISearchResource
    {
        private readonly IPackageRepository V2Client;
        public V2UISearchResource(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }
        public V2UISearchResource(IPackageRepository repo)
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

                    // V2 sometimes requires that we also use an OData filter for latest/latest prerelease version
                    if (filters.IncludePrerelease)
                    {
                        query = query.Where(p => p.IsAbsoluteLatestVersion);
                    }
                    else
                    {
                        query = query.Where(p => p.IsLatestVersion);
                    }

                    // execute the query
                    var allPackages = query
                        .Skip(skip)
                        .Take(take)
                        .ToList();

                    // Some V2 sources, e.g. NuGet.Server, local repository, the result contains all 
                    // versions of each package. So we need to explicitly select the latest version
                    // on the client side.
                    Dictionary<string, IPackage> latestVersion = new Dictionary<string, IPackage>(StringComparer.OrdinalIgnoreCase);

                    // this is used to maintain the order of the packages
                    List<string> packageIds = new List<string>();

                    foreach (var package in allPackages)
                    {
                        IPackage existingPackage;
                        if (latestVersion.TryGetValue(package.Id, out existingPackage))
                        {
                            if (package.Version > existingPackage.Version)
                            {
                                latestVersion[package.Id] = package;
                            }
                        }
                        else
                        {
                            latestVersion[package.Id] = package;
                            packageIds.Add(package.Id);
                        }
                    }

                    Queue<Task<UISearchMetadata>> tasks = new Queue<Task<UISearchMetadata>>();

                    // fetch version info in parallel
                    foreach (string id in packageIds)
                    {
                        tasks.Enqueue(CreatePackageSearchResult(latestVersion[id], filters, cancellationToken));
                    }

                    List<UISearchMetadata> results = new List<UISearchMetadata>();

                    while (tasks.Count > 0)
                    {
                        UISearchMetadata metadata = await tasks.Dequeue();
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

                string id = package.Id;
                NuGetVersion version = V2Utilities.SafeToNuGetVer(package.Version);
                string summary = package.Summary;

                var nuGetVersions = versions.Select(p =>
                    new VersionInfo(V2Utilities.SafeToNuGetVer(p.Version), p.DownloadCount));

                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = package.Description;
                }

                Uri iconUrl = package.IconUrl;
                PackageIdentity identity = new PackageIdentity(id, version);
                UISearchMetadata searchMetaData = new UISearchMetadata(identity, summary, iconUrl, nuGetVersions, V2UIMetadataResource.GetVisualStudioUIPackageMetadata(package));
                return searchMetaData;
            });
        }
      
    }
}