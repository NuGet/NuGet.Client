using BenchmarkDotNet.Attributes;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Protocol;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    [MemoryDiagnoser]

    public class Benchmarks
    {
        public Benchmarks()
        {
        }

        [GlobalSetup]
        public void Setup()
        {
        }

        [Benchmark]
        public void ConverterWithoutLoadJson()
        {
            var token = JToken.Parse(PackageRegistrationCatalogEntryWithDeprecationMetadata);
            PackageDependencyGroupConverter converter = new PackageDependencyGroupConverter();

            converter.ReadJson(token.CreateReader(), typeof(PackageDependencyGroup), null, null);
        }

        [Benchmark]
        public void ConverterUsingLoadJson()
        {
            var token = JToken.Parse(PackageRegistrationCatalogEntryWithDeprecationMetadata);
            PackageDependencyGroupConverter converter = new PackageDependencyGroupConverter();

            PackageDependencyGroupConverter.TestRead(token.CreateReader(), typeof(PackageDependencyGroup), null, null);
        }

        public const string PackageRegistrationCatalogEntryWithDeprecationMetadata = @"{
    ""@id"": ""https://apidev.nugettest.org/v3/catalog0/data/2019.06.25.23.44.13/afine.0.0.0.json"",
    ""@type"": ""PackageDetails"",
    ""authors"": ""scottbom"",
    ""dependencyGroups"": [
        {
            ""@id"": ""https://apidev.nugettest.org/v3/catalog0/data/2019.06.25.23.44.13/afine.0.0.0.json#dependencygroup"",
            ""@type"": ""PackageDependencyGroup"",
            ""dependencies"": [
                {
                    ""@id"": ""https://apidev.nugettest.org/v3/catalog0/data/2019.06.25.23.44.13/afine.0.0.0.json#dependencygroup/sampledependency"",
                    ""@type"": ""PackageDependency"",
                    ""id"": ""SampleDependency"",
                    ""range"": ""[1.0.0, )"",
                    ""registration"": ""https://apidev.nugettest.org/v3-registration3-gz-semver2/sampledependency/index.json""
                }
            ]
        }
    ],
    ""deprecation"": {
        ""@id"": ""https://apidev.nugettest.org/v3/catalog0/data/2019.06.25.23.44.13/afine.0.0.0.json#deprecation"",
        ""@type"": ""deprecation"",
        ""message"": ""this is a message"",
        ""reasons"": [
            ""CriticalBugs"",
            ""Legacy""
        ]
    },
    ""description"": ""A new package description"",
    ""iconUrl"": ""http://icon_url_here_or_delete_this_line/"",
    ""id"": ""afine"",
    ""language"": """",
    ""licenseExpression"": """",
    ""licenseUrl"": ""http://license_url_here_or_delete_this_line/"",
    ""listed"": true,
    ""minClientVersion"": """",
    ""packageContent"": ""https://apidev.nugettest.org/v3-flatcontainer/afine/0.0.0/afine.0.0.0.nupkg"",
    ""projectUrl"": ""http://project_url_here_or_delete_this_line/"",
    ""published"": ""2016-08-01T22:46:26.333+00:00"",
    ""requireLicenseAcceptance"": false,
    ""summary"": """",
    ""tags"": [
        ""Tag1"",
        ""Tag2""
    ],
    ""title"": """",
    ""version"": ""0.0.0""
}";
    }

    public class Program
    {

        public static void Main(string[] args)
        {
#if DEBUG
            var benchmark = new Benchmarks();
            benchmark.Setup();
            benchmark.ConverterWithoutLoadJson();
            benchmark.ConverterUsingLoadJson();
#else
            var summary = BenchmarkRunner.Run<Benchmarks>();
#endif
            // 16 content item, 8 128 - That's for lib.
            // 49 content items 8 98 - This is primarily runtime
            //Console.WriteLine(LockFileUtils.Count);
            //Console.WriteLine(ContentItem.Count);
            //foreach (var value in ContentItemCollection.Counts)
            //{
            //    Console.WriteLine(value.Key + " " + value.Value);
            //}
        }
    }
}
