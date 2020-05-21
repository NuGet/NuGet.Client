using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Test.Utility;
using NuGet.Protocol.Core.Types;
using NuGet.Common;
using System.Diagnostics;

namespace Nuget.Protocol.Benchmarking
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class PerformanceJsonParseTests
    {
        //private const bool Nuget = false;

        //private string ResourceName() = Nuget ? @"Nuget.Protocol.Benchmarking.nuget.json" : @"Nuget.Protocol.Benchmarking.dotnetfeed.blob.core.windows.net.json";
        private string ResourceName() => Nuget ? @"Nuget.Protocol.Benchmarking.nuget.json" : @"Nuget.Protocol.Benchmarking.dotnetfeed.blob.core.windows.net.json";

        //[GlobalSetup]
        //public void Setup()
        //{
        //}

        [Params(false, true)]
        public bool Nuget { get; set; }

        [Fact]
        //[Benchmark]
        public void JsonNetTest()
        {
            using var stream = GetEmbeddedSource(ResourceName());

            // Act
            var packages = DeserializeFromStream<FullPackageSearchMetadata>(stream);

            // Assert
            AssertPackages(packages.Data);
        }

        [Fact]
        //[Benchmark]
        public async Task<int> CurrentApproachTest()
        {
            var token = new CancellationToken();
            using var stream = GetEmbeddedSource(ResourceName());

            // Act
            var results = await stream.AsJObjectAsync(token);
            var data = results[JsonProperties.Data] as JArray ?? Enumerable.Empty<JToken>();
            var json = data.OfType<JObject>();
            var packages = json.Select(s => s.FromJToken<PackageSearchMetadata>()).ToList();

            // Assert
            AssertPackages(packages);

            return await Task<int>.FromResult(0);
        }


        private class FullPackageSearchMetadata
        {
            public List<PackageSearchMetadata> Data { get; set; }
        }

        private static T DeserializeFromStream<T>(Stream s)
        {
            using (StreamReader reader = new StreamReader(s))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = JsonExtensions.JsonObjectSerializer;
                return ser.Deserialize<T>(jsonReader);
            }
        }

        private void AssertPackages(List<PackageSearchMetadata> packages)
        {
            if (Nuget)
            {
                Assert.Equal(20, packages.Count);
            }
            else
            {
                Assert.Equal(1904, packages.Count);
                var package = packages.First();
                Assert.Equal("3.0.0-alpha-26807-18", package.ParsedVersions.First().Version.OriginalVersion);
                Assert.Equal("Accessibility", package.Identity.Id);
            }
        }

        private static Stream GetEmbeddedSource(string resoucename)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var stream = assembly.GetManifestResourceStream(resoucename);
            if (stream == null)
            {
                var items = assembly.GetManifestResourceNames();
                throw new Exception($"resource {resoucename} not found");
            }
            return stream;
        }

        [Fact]
        public async Task PackageSearchResourceV3_UnitTest()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            StreamReader reader = new StreamReader(GetEmbeddedSource(ResourceName()));
            var r = ResourceName();
            Debug.WriteLine(r);
            string text = reader.ReadToEnd();
            var take = 20;
            responses.Add($"https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take={take}&prerelease=false&semVerLevel=2.0.0",
                text);
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();
            resource.UseNew = 3;

            // Act
            var packages = await resource.SearchAsync(
                "entityframework",
                new SearchFilter(false),
                skip: 0,
                take: take,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            var package = packages.FirstOrDefault();
            var package2 = packages.Skip(1).FirstOrDefault();

            // Assert
            Assert.NotNull(package);
            Assert.Equal("Microsoft", package.Authors);
            //Assert.Equal("Component Object Model(COM) accessibility interfaces. \n63610380c84a0d348c63651e9194aac5a87738b0 \nWhen using NuGet 3.x this package requires at least version 3.4.", package.Description);
            //Assert.Equal(package.Description, package.Summary);
            //Assert.Equal("EntityFramework", package.Title);
            //Assert.Equal(string.Join(", ", "Microsoft", "EF", "Database", "Data", "O/RM", "ADO.NET"), package.Tags);
            //Assert.Equal("Component Object Model(COM) accessibility interfaces. \n63610380c84a0d348c63651e9194aac5a87738b0 \nWhen using NuGet 3.x this package requires at least version 3.4.", package.Description);

            Assert.NotNull(package2);
            Assert.Equal("Microsoft", package2.Authors);
            Assert.Equal("Package Description", package2.Description);
            Assert.Equal(package.Description, package2.Summary);
            Assert.Equal("AppWithOutputAssemblyName", package2.Title);
            Assert.Equal("", package2.Tags);
            Assert.False(package2.PrefixReserved);
        }

        public async Task PackageSearchResourceV3_Test(int testNo)
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            StreamReader reader = new StreamReader(GetEmbeddedSource(ResourceName()));
            var r = ResourceName();
            Debug.WriteLine(r);
            string text = reader.ReadToEnd();
            var take = 20;
            responses.Add($"https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take={take}&prerelease=false&semVerLevel=2.0.0",
                text);
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();
            resource.UseNew = testNo;

            // Act
            var packages = await resource.SearchAsync(
                "entityframework",
                new SearchFilter(false),
                skip: 0,
                take: take,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            var package = packages.FirstOrDefault();
            var package2 = packages.Skip(1).FirstOrDefault();

            // Assert
            Assert.NotNull(package);
            Assert.Equal("Microsoft", package.Authors);
            //Assert.Equal("Component Object Model(COM) accessibility interfaces. \n63610380c84a0d348c63651e9194aac5a87738b0 \nWhen using NuGet 3.x this package requires at least version 3.4.", package.Description);
            //Assert.Equal(package.Description, package.Summary);
            //Assert.Equal("EntityFramework", package.Title);
            //Assert.Equal(string.Join(", ", "Microsoft", "EF", "Database", "Data", "O/RM", "ADO.NET"), package.Tags);
            //Assert.Equal("Component Object Model(COM) accessibility interfaces. \n63610380c84a0d348c63651e9194aac5a87738b0 \nWhen using NuGet 3.x this package requires at least version 3.4.", package.Description);

            Assert.NotNull(package2);
            Assert.Equal("Microsoft", package2.Authors);
            Assert.Equal("Package Description", package2.Description);
            Assert.Equal(package.Description, package2.Summary);
            Assert.Equal("AppWithOutputAssemblyName", package2.Title);
            Assert.Equal("", package2.Tags);
            Assert.False(package2.PrefixReserved);
        }

        [Fact]
        [Benchmark(Description = "Test0:OldImplementation", Baseline =true)]
        public async Task PackageSearchResourceV3_GetMetadataAsync0()
        {
            PackageSearchResourceV3_Test(0);
        }

        [Fact]
        [Benchmark(Description = "Test1:joelverhagen")]
        public async Task PackageSearchResourceV3_GetMetadataAsync1()
        {
            PackageSearchResourceV3_Test(1);
        }

        [Fact]
        [Benchmark(Description = "Test2:partialStream")]
        public async Task PackageSearchResourceV3_GetMetadataAsync2()
        {
            PackageSearchResourceV3_Test(2);
        }

        [Fact]
        [Benchmark(Description = "Test3:zivkan")]
        public async Task PackageSearchResourceV3_GetMetadataAsync3()
        {
            PackageSearchResourceV3_Test(3);
        }
    }
}
