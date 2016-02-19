using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class MetadataResourceV2FeedTests
    {
        [Fact]
        public async Task MetaDataResourceGetLatestVersion()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var latestVersion = await metadataResource.GetLatestVersion("WindowsAzure.Storage", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("6.2.2-preview", latestVersion.ToNormalizedString());
        }

        [Fact]
        public async Task MetaDataResourceGetLatestVersionStable()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var latestVersion = await metadataResource.GetLatestVersion("WindowsAzure.Storage", false, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("6.2.0", latestVersion.ToNormalizedString());
        }

        [Fact]
        public async Task MetaDataResourceGetVersionsStable()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var versions = await metadataResource.GetVersions("WindowsAzure.Storage", false, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(34, versions.Count());
        }

        [Fact]
        public async Task MetaDataResourceGetVersions()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var versions = await metadataResource.GetVersions("WindowsAzure.Storage", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(44, versions.Count());
        }

        [Fact]
        public async Task MetaDataResourceIdExist()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var exist = await metadataResource.Exists("WindowsAzure.Storage", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.True(exist);
        }

        [Fact]
        public async Task MetaDataResourceIdentityExist()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/Packages(Id='WindowsAzure.Storage',Version='4.3.2-preview')",
                  TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageGetPackages.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            var package = new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.2-preview"));

            // Act
            var exist = await metadataResource.Exists(package, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.True(exist);
        }

        [Fact]
        public async Task MetaDataResourceGetLatestVersions()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));
            responses.Add("http://testsource/v2/FindPackagesById()?Id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            var packageIdList = new List<string>() { "WindowsAzure.Storage", "xunit" };

            // Act
            var versions = (await metadataResource.GetLatestVersions(packageIdList, true, false, NullLogger.Instance, CancellationToken.None)).ToList();

            // Assert
            Assert.Equal("WindowsAzure.Storage", versions[1].Key);
            Assert.Equal("6.2.2-preview", versions[1].Value.ToNormalizedString());
            Assert.Equal("xunit", versions[0].Key);
            Assert.Equal("2.2.0-beta1-build3239", versions[0].Value.ToNormalizedString());
        }

        [Fact]
        public async Task MetaDataResourceGetLatestVersionInvalidId()
        {
            // Arrange

            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='not-found'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.NotFoundFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var latestVersion = await metadataResource.GetLatestVersion("not-found", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Null(latestVersion);
        }

        [Fact]
        public async Task MetaDataResourceGetVersionsInvalidId()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='not-found'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.NotFoundFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var versions = await metadataResource.GetVersions("not-found", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(0, versions.Count());
        }

        [Fact]
        public async Task MetaDataResourceIdentityExistInvalidIdentity()
        {
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/Packages(Id='xunit',Version='1.0.0-notfound')", null);
            responses.Add("http://testsource/v2/FindPackagesById()?Id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses,
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            var package = new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound"));

            // Act
            var exist = await metadataResource.Exists(package, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.False(exist);
        }

        [Fact]
        public async Task MetaDataResourceIdExistNotFoundId()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='not-found'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.NotFoundFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            // Act
            var exist = await metadataResource.Exists("not-found", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.False(exist);
        }
    }
}
