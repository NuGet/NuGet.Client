using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio.Converters;
using NuGet.Protocol.VisualStudio.Services;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace NuGet.Protocol.VisualStudio.Tests
{
    public class PackageSearchResponse
    {
        public int totalHits;
        public string index;
        public PackageSearchMetadata[] data;
    }

    public class SearchResultsAggregatorTests
    {
        [Fact]
        public void Test()
        {
           var indexer = new LuceneSearchResultsIndexer();
            var aggregator = new SearchResultsAggregator(indexer);

            var queryString = "nuget";
            var rawSearch1 = LoadTestResponse("searchResults1.json");
            var rawSearch2 = LoadTestResponse("searchResults2.json");
            var results = aggregator.Aggregate(queryString, rawSearch1.data, rawSearch2.data);

            Assert.NotEmpty(results);
        }

        private static PackageSearchResponse LoadTestResponse(string fileName)
        {
            var assembly = typeof(SearchResultsAggregatorTests).Assembly;

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter { CamelCaseText = false },
                    new PackageSearchMetadataConverter()
                }
            };
            var serializer = JsonSerializer.Create(settings);

            var resourcePath = string.Join(".", typeof(SearchResultsAggregatorTests).Namespace, "Resources", fileName);
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
