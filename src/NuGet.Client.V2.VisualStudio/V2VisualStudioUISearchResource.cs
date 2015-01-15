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

        private async Task<IEnumerable<UISearchMetadata>> GetSearchResultsForVisualStudioUI(string searchTerm, SearchFilter filters, int skip, int take, System.Threading.CancellationToken cancellationToken)
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
            return (IEnumerable<UISearchMetadata>)query
                .Skip(skip)
                .Take(take)
                .ToList()
                .AsParallel()
                .AsOrdered()
                .Select(p => CreatePackageSearchResult(p, cancellationToken))
                .ToList();
        }

        private UISearchMetadata CreatePackageSearchResult(IPackage package, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var versions = V2Client.FindPackagesById(package.Id);
            if (!versions.Any())
            {
                versions = new[] { package };
            }
            string id = package.Id;
            NuGetVersion version = V2Utilities.SafeToNuGetVer(package.Version);
            string summary = package.Summary;
            IEnumerable<NuGetVersion> nuGetVersions = versions.Select(p => V2Utilities.SafeToNuGetVer(p.Version));
            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = package.Description;
            }

            Uri iconUrl = package.IconUrl;
            PackageIdentity identity = new PackageIdentity(id, version);
            UISearchMetadata searchMetaData = new UISearchMetadata(identity, summary, iconUrl, nuGetVersions, null);
            return searchMetaData;
        }
      
    }
}