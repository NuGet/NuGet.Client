using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.FuncTest
{
    public class V2FeedParserTests
    {
        [Fact]
        public async Task V2FeedParser_DownloadFromInvalidUrl()
        {
            // Arrange
            var randomName = Guid.NewGuid().ToString();
            var repo = Repository.Factory.GetCoreV3(TestServers.NuGetV2);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, TestServers.NuGetV2);

            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await parser.DownloadFromUrl(new PackageIdentity("not-found", new NuGetVersion("6.2.0")),
                                                              new Uri($"https://www.{randomName}.org/api/v2/"),
                                                              Configuration.NullSettings.Instance,
                                                              new SourceCacheContext(),
                                                              NullLogger.Instance,
                                                              CancellationToken.None));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal($"Error downloading 'not-found.6.2.0' from 'https://www.{randomName}.org/api/v2/'.", ex.Message);
        }

        [Fact]
        public async Task V2FeedParser_DownloadFromUrlInvalidId()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(TestServers.NuGetV2);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, TestServers.NuGetV2);

            // Act 
            var actual = await parser.DownloadFromUrl(new PackageIdentity("not-found", new NuGetVersion("6.2.0")),
                new Uri($@"{TestServers.NuGetV2}/package/not-found/6.2.0"),
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(DownloadResourceResultStatus.NotFound, actual.Status);
        }

        [Fact]
        public async Task V2FeedParser_DownloadFromIdentity()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(TestServers.NuGetV2);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, TestServers.NuGetV2);

            // Act & Assert
            using (var downloadResult = await parser.DownloadFromIdentity(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                                                              Configuration.NullSettings.Instance,
                                                              new SourceCacheContext(),
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

        [Theory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
        public async Task V2FeedParser_NormalizedVersion(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);
            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            // Act
            var package = await parser.GetPackage(new PackageIdentity("owin", new NuGetVersion("1.0")), NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("Owin", package.Id);
            Assert.Equal("1.0", package.Version.ToString());
        }

        [Theory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
        public async Task V2FeedParser_DownloadFromIdentityFromDifferentServer(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            // Act & Assert
            using (var downloadResult = await parser.DownloadFromIdentity(new PackageIdentity("newtonsoft.json", new NuGetVersion("8.0.3")),
                                                              Configuration.NullSettings.Instance,
                                                              new SourceCacheContext(),
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(15, files.Count());
            }
        }

        // ProGet does not support seach portable framework, it will return empty packages
        [Theory]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
        public async Task V2FeedParser_SearchWithPortableFramework(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "portable-net45+win8" }
            };

            // Act
            var packages = await parser.Search("nunit", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("NUnit", package.Id);
        }

        [Theory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
        public async Task V2FeedParser_Search(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net45" }
            };

            // Act
            var packages = await parser.Search("nunit", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("NUnit", package.Id);
        }

        [Theory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
        public async Task V2FeedParser_SearchWithPrerelease(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = true,
                SupportedFrameworks = new string[] { "net" }
            };

            // Act
            var packages = await parser.Search("entityframework", searchFilter, 0, 3, NullLogger.Instance, CancellationToken.None);
            var package = packages.Where(p => p.Id == "EntityFramework" && p.Version.ToString() == "7.0.0-beta4").FirstOrDefault();

            // Assert
            Assert.NotNull(package);
        }

        [Theory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task V2FeedParser_CredentialNormalizedVersion(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);
            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            // Act
            var package = await parser.GetPackage(new PackageIdentity("owin", new NuGetVersion("1.0")), NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("Owin", package.Id);
            Assert.Equal("1.0", package.Version.ToString());
        }

        [Theory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task V2FeedParser_DownloadFromIdentityFromDifferentCredentialServer(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            // Act & Assert
            using (var downloadResult = await parser.DownloadFromIdentity(new PackageIdentity("newtonsoft.json", new NuGetVersion("8.0.3")),
                                                              Configuration.NullSettings.Instance,
                                                              new SourceCacheContext(),
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(15, files.Count());
            }
        }

        [Theory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task V2FeedParser_SearchWithPortableFrameworkFromCredentialServer(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "portable-net45+win8" }
            };

            // Act
            var packages = await parser.Search("nunit", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("NUnit", package.Id);
        }

        [Theory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task V2FeedParser_SearchFromCredentialServer(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net45" }
            };

            // Act
            var packages = await parser.Search("nunit", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("NUnit", package.Id);
        }

        [Theory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task V2FeedParser_SearchWithPrereleaseCredentialServer(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, packageSource);

            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = true,
                SupportedFrameworks = new string[] { "net" }
            };

            // Act
            var packages = await parser.Search("entityframework", searchFilter, 0, 3, NullLogger.Instance, CancellationToken.None);
            var package = packages.Where(p => p.Id == "EntityFramework" && p.Version.ToString() == "7.0.0-beta4").FirstOrDefault();

            // Assert
            Assert.NotNull(package);
        }
    }
}
