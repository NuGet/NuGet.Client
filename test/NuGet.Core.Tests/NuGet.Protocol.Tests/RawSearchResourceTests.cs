// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RawSearchResourceTests
    {
        [Fact]
        public async Task RawSearchResource_SearchEncoding()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var searchResource = new RawSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            var skip = 0;
            var take = 1;
            // Act
            var packages = await searchResource.Search(
                    (_client, uri) => httpSource.ProcessHttpStreamAsync(
                        new HttpSourceRequest(uri, Common.NullLogger.Instance),
                        s => searchResource.ProcessHttpStreamTakeCountedItemAsync(s, take, CancellationToken.None),
                        NullLogger.Instance,
                        CancellationToken.None),
                        "azure b",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        CancellationToken.None);

            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            Assert.True(packagesArray.Length > 0);
        }

        [Fact]
        public async Task RawSearchResource_VerifyReadSyncIsNotUsed()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            // throw if sync .Read is used
            httpSource.StreamWrapper = (stream) => new NoSyncReadStream(stream);

            var searchResource = new RawSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };
            var skip = 0;
            var take = 1;
            // Act
            var packages = await searchResource.Search(
                    (_client, uri) => httpSource.ProcessHttpStreamAsync(
                        new HttpSourceRequest(uri, Common.NullLogger.Instance),
                        s => searchResource.ProcessHttpStreamTakeCountedItemAsync(s, take, CancellationToken.None),
                        NullLogger.Instance,
                        CancellationToken.None),
                        "azure b",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        CancellationToken.None);
            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            // Verify no failures from Sync Read
            Assert.True(packagesArray.Length > 0);
        }

        [Fact]
        public async Task RawSearchResource_CancelledToken_ThrowsOperationCancelledException()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            httpSource.StreamWrapper = (stream) => new NoSyncReadStream(stream);

            var searchResource = new RawSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var skip = 0;
            var take = 1;
            // Act/Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
               searchResource.Search(
                    (_client, uri) => httpSource.ProcessHttpStreamAsync(
                        new HttpSourceRequest(uri, Common.NullLogger.Instance),
                        s => searchResource.ProcessHttpStreamTakeCountedItemAsync(s, take, tokenSource.Token),
                        NullLogger.Instance,
                        tokenSource.Token),
                        "Sentry",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        tokenSource.Token));

        }
    }
}
