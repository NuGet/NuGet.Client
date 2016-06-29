using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class DownloadResourceV2FeedTests
    {
        [Fact]
        public async Task DownloadResourceFromIdentityInvalidId()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='xunit',Version='1.0.0-notfound')", string.Empty);
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses,
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            // Act 
            var actual = await downloadResource.GetDownloadResourceResultAsync(new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound")),
                NullSettings.Instance,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(DownloadResourceResultStatus.NotFound, actual.Status);
        }
    }
}
