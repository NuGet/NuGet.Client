﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public class RemoteV2FindPackageByIdResourceTests
    {
        [Fact]
        public async Task RemoteV2FindPackageById_VerifyNoErrorsOnNoContent()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='a'", "204");

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
            resource.Logger = NullLogger.Instance;
            resource.CacheContext = new SourceCacheContext();

            // Act
            var versions = await resource.GetAllVersionsAsync("a", CancellationToken.None);

            // Assert
            // Verify no items returned, and no exceptions were thrown above
            Assert.Equal(0, versions.Count());
        }

        [Fact]
        public async Task RemoteV2FindPackageById_GetOriginalIdentity_IdInResponse()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var serviceAddress = TestUtility.CreateServiceAddress();
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "xunit", "2.2.0-beta1-build3239");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        serviceAddress,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(string.Empty)
                        })
                    },
                    {
                        serviceAddress + "FindPackagesById()?id='XUNIT'",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()))
                        })
                    },
                    {
                        "https://www.nuget.org/api/v2/package/xunit/2.2.0-beta1-build3239",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

                var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
                resource.Logger = NullLogger.Instance;
                resource.CacheContext = new SourceCacheContext();

                // Act
                var identity = await resource.GetOriginalIdentityAsync(
                    "XUNIT",
                    new NuGetVersion("2.2.0-BETA1-build3239"),
                    CancellationToken.None);

                // Assert
                Assert.IsType<RemoteV2FindPackageByIdResource>(resource);
                Assert.Equal("xunit", identity.Id);
                Assert.Equal("2.2.0-beta1-build3239", identity.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task RemoteV2FindPackageById_GetOriginalIdentity_IdNotInResponse()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var serviceAddress = TestUtility.CreateServiceAddress();
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "WindowsAzure.Storage", "6.2.2-preview");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        serviceAddress,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(string.Empty)
                        })
                    },
                    {
                        serviceAddress + "FindPackagesById()?id='WINDOWSAZURE.STORAGE'",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()))
                        })
                    },
                    {
                        "https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.2-preview",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

                var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
                resource.Logger = NullLogger.Instance;
                resource.CacheContext = new SourceCacheContext();

                // Act
                var identity = await resource.GetOriginalIdentityAsync(
                    "WINDOWSAZURE.STORAGE",
                    new NuGetVersion("6.2.2-PREVIEW"),
                    CancellationToken.None);

                // Assert
                Assert.IsType<RemoteV2FindPackageByIdResource>(resource);
                Assert.Equal("WindowsAzure.Storage", identity.Id);
                Assert.Equal("6.2.2-preview", identity.Version.ToNormalizedString());
            }
        }
    }
}