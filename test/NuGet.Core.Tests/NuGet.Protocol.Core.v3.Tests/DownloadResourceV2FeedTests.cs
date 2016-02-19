using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class DownloadResourceV2FeedTests
    {
        [Fact]
        public async Task DownloadResourceFromUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new SourcePackageDependencyInfo("WindowsAzure.Storage", new NuGetVersion("6.2.0"), null, true, repo, new Uri("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0"), "");

            // Act & Assert
            using (var downloadResult = await downloadResource.GetDownloadResourceResultAsync(package,
                                                              NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

        [Fact]
        public async Task DownloadResourceFromIdentity()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0"));

            // Act & Assert
            using (var downloadResult = await downloadResource.GetDownloadResourceResultAsync(package,
                                                              NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

        [Fact]
        public async Task DownloadResourceFromIdentityInvalidId()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/Packages(Id='xunit',Version='1.0.0-notfound')", null);
            responses.Add("http://testsource/v2/FindPackagesById()?Id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses,
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await downloadResource.GetDownloadResourceResultAsync(new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound")),
                                                              Configuration.NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Can't find Package 'xunit.1.0.0-notfound' from source 'http://testsource/v2/'.", ex.Message);
        }

        [Fact]
        public async Task DownloadResourceFromInvalidIdInUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new SourcePackageDependencyInfo("not-found", new NuGetVersion("6.2.0"), null, true, repo, new Uri("https://www.nuget.org/api/v2/package/not-found/6.2.0"), "");

            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await downloadResource.GetDownloadResourceResultAsync(package,
                                                              NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Error downloading 'not-found.6.2.0' from 'https://www.nuget.org/api/v2/package/not-found/6.2.0'.", ex.Message);
        }

        [Fact]
        public async Task DownloadResourceFromInvalidUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.invalid.org/api/v2");

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new SourcePackageDependencyInfo("not-found", new NuGetVersion("6.2.0"), null, true, repo, new Uri("https://www.invalid.org/api/v2/package/not-found/6.2.0"), "");

            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await downloadResource.GetDownloadResourceResultAsync(package,
                                                              NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Error downloading 'not-found.6.2.0' from 'https://www.invalid.org/api/v2/package/not-found/6.2.0'.", ex.Message);
        }

        [Fact]
        public async Task DownloadResourceFromIdentityInvalidSource()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.invalid.org/api/v2/");

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new PackageIdentity("not-found", new NuGetVersion("6.2.0"));

            // Act & Assert
            // Act 
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await downloadResource.GetDownloadResourceResultAsync(package,
                                                              NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None));

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Error downloading 'not-found.6.2.0' from 'https://www.invalid.org/api/v2/'.", ex.Message);
        }
    }
}
