// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
            using (var workingDir = TestDirectory.Create())
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
                var logger = new TestLogger();

                using (var cacheContext = new SourceCacheContext())
                {
                    var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    // Act
                    var info = await resource.GetDependencyInfoAsync(
                        "DEEPEQUAL",
                        new NuGetVersion("1.4.0.1-RC"),
                        cacheContext,
                        logger,
                        CancellationToken.None);

                    // Assert
                    Assert.IsType<HttpFileSystemBasedFindPackageByIdResource>(resource);
                    Assert.Equal("DeepEqual", info.PackageIdentity.Id);
                    Assert.Equal("1.4.0.1-rc", info.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }
    }
}
