using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    public class V2SimpleSearchResource : SimpleSearchResource
    {
        private readonly IPackageRepository V2Client;
        public V2SimpleSearchResource(IPackageRepository repo)
        {
            V2Client = repo;
        }
        public V2SimpleSearchResource(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }
        public override Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, System.Threading.CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
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
                return (IEnumerable<SimpleSearchMetadata>)query
                    .Skip(skip)
                    .Take(take)
                    .ToList()
                    .AsParallel()
                    .AsOrdered()
                    .Select(p => CreatePackageSearchResult(p))
                    .ToList();
            }, cancellationToken);
        }
        private SimpleSearchMetadata CreatePackageSearchResult(IPackage package)
        {          
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
            SimpleSearchMetadata searchMetaData = new SimpleSearchMetadata(identity, summary, nuGetVersions);
            return searchMetaData;
        }
    }
}
