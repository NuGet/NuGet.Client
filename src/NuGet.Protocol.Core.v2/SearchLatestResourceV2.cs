using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v2
{
    public class SearchLatestResourceV2 : SearchLatestResource
    {
        private readonly IPackageRepository _repository;

        public SearchLatestResourceV2(V2Resource resource)
        {
            _repository = resource.V2Client;
        }

        public override async Task<IEnumerable<ServerPackageMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
                {
                    var query = _repository.Search(
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
                    List<IPackage> allPackages = query
                        .Skip(skip)
                        .Take(take)
                        .ToList();

                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    List<ServerPackageMetadata> results = new List<ServerPackageMetadata>();

                    foreach (var package in allPackages)
                    {
                        if (seen.Add(package.Id))
                        {
                            var highest = allPackages.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, package.Id))
                                    .OrderByDescending(p => p.Version).First();

                            ServerPackageMetadata metadata = ParsePackageMetadataV2.Parse(highest);
                            results.Add(metadata);
                        }
                    }

                    return results;
                });
        }
    }
}
