using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Indexing.Test
{
    public class PackageSearchResponse
    {
        public int totalHits;
        public string index;
        public PackageSearchMetadata[] data;
    }

    public class SearchResultsAggregatorTests
    {
        private const string NuGetCore = "NuGet.Core";

        [Fact]
        public async Task AggregateAsync_MergesVersions()
        {
            var indexer = new LuceneSearchResultsIndexer();
            var aggregator = new SearchResultsAggregator(indexer);

            var queryString = "nuget";
            var rawSearch1 = LoadTestResponse("searchResults1.json");
            var package1 = FindNuGetCorePackage(rawSearch1.data);
            var v1 = await GetPackageVersionsAsync(package1);
            Assert.NotEmpty(v1);

            var rawSearch2 = LoadTestResponse("searchResults2.json");
            var package2 = FindNuGetCorePackage(rawSearch2.data);
            var v2 = await GetPackageVersionsAsync(package2);
            Assert.NotEmpty(v2);

            var results = await aggregator.AggregateAsync(queryString, rawSearch1.data, rawSearch2.data);

            var mergedPackage = FindNuGetCorePackage(results);
            var vm = await GetPackageVersionsAsync(mergedPackage);
            Assert.Superset(v1, vm);
            Assert.Superset(v2, vm);
        }

        private static async Task<ISet<NuGetVersion>> GetPackageVersionsAsync(IPackageSearchMetadata package)
        {
            return new HashSet<NuGetVersion>((await package.GetVersionsAsync()).Select(v => v.Version));
        }

        private static IPackageSearchMetadata FindNuGetCorePackage(IEnumerable<IPackageSearchMetadata> searchResults)
        {
            return searchResults
                .First(p => string.Equals(p.Identity.Id, NuGetCore, StringComparison.OrdinalIgnoreCase));
        }

        private static PackageSearchResponse LoadTestResponse(string fileName)
        {
            var assembly = typeof(SearchResultsAggregatorTests).Assembly;

            var serializer = JsonSerializer.Create(JsonExtensions.ObjectSerializationSettings);

            var resourcePath = string.Join(".", typeof(SearchResultsAggregatorTests).Namespace, "compiler.resources", fileName);
            using (var stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var streamReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    return serializer.Deserialize<PackageSearchResponse>(jsonReader);
                }
            }
        }
    }
}
