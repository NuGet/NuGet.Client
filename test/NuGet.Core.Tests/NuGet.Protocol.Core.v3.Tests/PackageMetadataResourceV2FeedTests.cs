using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class PackageMetadataResourceV2FeedTests
    {
        [Fact]
        public async Task PackageMetadataResource_Basic()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var packageMetadataResource = await repo.GetResourceAsync<PackageMetadataResource>();

            // Act
            var metadata = await packageMetadataResource.GetMetadataAsync("WindowsAzure.Storage", true, false, NullLogger.Instance, CancellationToken.None);
            var latestPackage = metadata.OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault();

            // Assert
            Assert.Equal(44, metadata.Count());

            Assert.Equal("WindowsAzure.Storage", latestPackage.Identity.Id);
            Assert.Equal("6.2.2-preview", latestPackage.Identity.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", latestPackage.Title);
            Assert.Equal("Microsoft", latestPackage.Authors);
            Assert.Equal("", latestPackage.Owners);
            Assert.True(latestPackage.Description.StartsWith("This client library enables"));
            Assert.Equal(3957668, latestPackage.DownloadCount);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", latestPackage.IconUrl.AbsoluteUri);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", latestPackage.LicenseUrl.AbsoluteUri);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", latestPackage.ProjectUrl.AbsoluteUri);
            Assert.Equal(DateTimeOffset.Parse("2015-12-11T01:25:11.37"), latestPackage.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/6.2.2-preview", latestPackage.ReportAbuseUrl.AbsoluteUri);
            Assert.True(latestPackage.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", latestPackage.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", latestPackage.Tags);
            Assert.Equal(6, latestPackage.DependencySets.Count());
            Assert.Equal("dotnet5.4", latestPackage.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task PackageMetadataResource_NotFound()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/FindPackagesById()?Id='not-found'",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.NotFoundFindPackagesById.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var packageMetadataResource = await repo.GetResourceAsync<PackageMetadataResource>();

            // Act
            var metadata = await packageMetadataResource.GetMetadataAsync("not-found", true, false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(0, metadata.Count());
        }
    }
}
