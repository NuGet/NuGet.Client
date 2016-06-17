using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpFileSystemBasedFindPackageByIdResourceTests
    {
        [Fact]
        public async Task HttpFileSystemBasedFindPackageById_GetOriginalIdentity()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var source = "http://testsource.com/v3-with-flat-container/index.json";
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "DeepEqual", "1.4.0.1-rc");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        source,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.IndexWithFlatContainer)
                        })
                    },
                    {
                        "https://api.nuget.org/v3-flatcontainer/deepequal/index.json",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.DeepEqualFlatContainerIndex)
                        })
                    },
                    {
                        "https://api.nuget.org/v3-flatcontainer/deepequal/1.4.0.1-rc/deepequal.1.4.0.1-rc.nupkg",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

                var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
                resource.Logger = NullLogger.Instance;
                resource.CacheContext = new SourceCacheContext();

                // Act
                var identity = await resource.GetOriginalIdentityAsync(
                    "DEEPEQUAL",
                    new NuGetVersion("1.4.0.1-RC"),
                    CancellationToken.None);

                // Assert
                Assert.IsType<HttpFileSystemBasedFindPackageByIdResource>(resource);
                Assert.Equal("DeepEqual", identity.Id);
                Assert.Equal("1.4.0.1-rc", identity.Version.ToNormalizedString());
            }
        }
    }
}
