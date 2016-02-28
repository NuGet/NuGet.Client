using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.FuncTest
{
    public class V2FeedParserTests
    {
        [Fact]
        public async Task V2FeedParser_DownloadFromInvalidUrl()
        {
            // Arrange
            var randomName = Guid.NewGuid().ToString();
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await parser.DownloadFromUrl(new PackageIdentity("not-found", new NuGetVersion("6.2.0")),
                                                              new Uri($"https://www.{randomName}.org/api/v2/"),
                                                              Configuration.NullSettings.Instance,
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
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            // Act 
            var actual = await parser.DownloadFromUrl(new PackageIdentity("not-found", new NuGetVersion("6.2.0")),
                new Uri("https://www.nuget.org/api/v2/package/not-found/6.2.0"),
                Configuration.NullSettings.Instance,
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
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            // Act & Assert
            using (var downloadResult = await parser.DownloadFromIdentity(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                                                              Configuration.NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

    }
}
