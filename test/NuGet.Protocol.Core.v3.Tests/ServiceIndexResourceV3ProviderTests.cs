// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class ServiceIndexResourceV3ProviderTests
    {
        [Theory]
        [InlineData(@"d:\packages\mypackages.json")]
        [InlineData(@"\\network\mypackages.json")]
        [InlineData(@"/mypackages/mypackages.json")]
        [InlineData(@"~/mypackages/mypackages.json")]
        public async Task TryCreate_ReturnsFalse_IfSourceIsADirectory(string location)
        {
            // Arrange
            var packageSource = new PackageSource(location);
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(packageSource, new[] { provider });

            // Act
            var result = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert
            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://www.nuget.org/api/v2.json/")]
        public async Task TryCreate_ReturnsFalse_IfSourceDoesNotHaveAJsonSuffix(string location)
        {
            // Arrange
            var packageSource = new PackageSource(location);
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(packageSource, new[] { provider });

            // Act
            var result = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert
            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task TryCreate_Throws_IfSourceLocationReturnsFailureCode()
        {
            // Arrange
            var source = "https://does-not-exist.server/does-not-exist.json";
            // This will return a 404 - NotFound.
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(new Dictionary<string, string> { { source, string.Empty } });
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { handlerProvider, provider });

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                var result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            });
        }

        [Theory]
        [InlineData("not-valid-json")]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?><service xml:base=""http://www.nuget.org/api/v2/"" 
xmlns=""http://www.w3.org/2007/app"" xmlns:atom=""http://www.w3.org/2005/Atom""><workspace><atom:title>Default</atom:title>
<collection href=""Packages""><atom:title>Packages</atom:title></collection></workspace></service>")]
        public async Task TryCreate_Throws_IfSourceLocationDoesNotReturnValidJson(string content)
        {
            // Arrange
            var source = "https://fake.server/users.json";
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(new Dictionary<string, string> { { source, content } });
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { handlerProvider, provider });

            // Act and assert
            await Assert.ThrowsAsync<JsonReaderException> (async () =>
            {
                var result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            });
        }

        [Theory]
        [InlineData("{ version: \"not-semver\" } ")]
        [InlineData("{ version: \"3.0.0.0\" } ")] // not strict semver
        public async Task TryCreate_Throws_IfInvalidVersionInJson(string content)
        {
            // Arrange
            var source = "https://fake.server/users.json";
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(new Dictionary<string, string> { { source, content } });
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { handlerProvider, provider });

            // Act
            NuGetProtocolException ex = await Assert.ThrowsAsync<NuGetProtocolException>(async () =>
            {
                var result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            });

            // Assert
            Assert.True(ex.Message.StartsWith("The source version is not supported"));
        }

        [Theory]
        [InlineData("{ json: \"that does not contain version.\" }")]
        [InlineData("{ version: 3 } ")] // version is not a string
        [InlineData("{ version: { value: 3 } } ")] // version is not a string
        public async Task TryCreate_Throws_IfNoVersionInJson(string content)
        {
            // Arrange
            var source = "https://fake.server/users.json";
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(new Dictionary<string, string> { { source, content } });
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { handlerProvider, provider });

            // Act
            NuGetProtocolException ex = await Assert.ThrowsAsync<NuGetProtocolException>(async () =>
            {
                var result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            });

            // Assert
            Assert.Equal(ex.Message, "The source does not have the 'version' property.");
        }

        [Fact]
        public async Task TryCreate_ReturnsTrue_IfSourceLocationReturnsValidJson()
        {
            // Arrange
            var source = "https://some-site.org/test.json";
            var content = @"{ version: '3.1.0-beta' }";
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(new Dictionary<string, string> { { source, content } });
            var provider = new ServiceIndexResourceV3Provider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { handlerProvider, provider });

            // Act
            var result = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert
            Assert.True(result.Item1);
            var resource = Assert.IsType<ServiceIndexResourceV3>(result.Item2);
            Assert.NotNull(resource.Index);
        }

        // [Fact]
        // Need a new test for this scenario
        public async Task TryCreate_CachesResultsForFortyMinutes()
        {
            // Arrange
            var source = "http://some-site/index.json";
            var content = @"{ version: '3.1.0-beta' }";
            var handler = new Mock<TestMessageHandler>(new Dictionary<string, string> { { source, content } })
            {
                CallBase = true
            };
            var sequence = new MockSequence();
            handler
                .InSequence(sequence)
                .Setup(h => h.SendAsyncPublic(It.IsAny<HttpRequestMessage>()))
                .Returns(() =>
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                });

            handler
                .InSequence(sequence)
                .Setup(h => h.SendAsyncPublic(It.IsAny<HttpRequestMessage>()))
                .Returns(() =>
                {
                    var response = new HttpResponseMessage();
                    response.Content = new StringContent(content);
                    return Task.FromResult(response);
                });

            var provider = new TestableSerivceIndexResourceProvider();
            var sourceRepository = new SourceRepository(new PackageSource(source),
                new INuGetResourceProvider[] { new TestHttpHandlerProvider(handler.Object), provider });
            var startTime = DateTime.UtcNow;

            // Act - 1
            var result = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert - 1
            Assert.False(result.Item1);
            Assert.Null(result.Item2);

            // Act - 2
            var minutesago45 = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(45));

            var index = new ServiceIndexResourceV3(new JObject(), minutesago45);

            provider.ReplaceCache(source, minutesago45, index);

            result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            var result2 = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert - 2
            Assert.True(result.Item1);
            Assert.True(index != result.Item2);         // Previous entry was expired
            Assert.True(result.Item2 == result2.Item2); // Same as last request

            // Act - 3
            var minutesago20 = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(20));

            index = new ServiceIndexResourceV3(new JObject(), minutesago20);

            provider.ReplaceCache(source, minutesago20, index);

            result = await provider.TryCreate(sourceRepository, default(CancellationToken));
            result2 = await provider.TryCreate(sourceRepository, default(CancellationToken));

            // Assert - 3
            Assert.True(result.Item1);
            Assert.True(result2.Item1);
            Assert.True(index == result.Item2);   // Same
            Assert.True(index == result2.Item2);  // Same
        }

        private class TestableSerivceIndexResourceProvider : ServiceIndexResourceV3Provider
        {
            public void SetDuration(TimeSpan span)
            {
                MaxCacheDuration = span;
            }

            public void ReplaceCache(string url, DateTime date, ServiceIndexResourceV3 index)
            {
                _cache.Clear();

                var entry = new ServiceIndexCacheInfo()
                {
                    CachedTime = date,
                    Index = index
                };

                _cache.TryAdd(url, entry);
            }
        }
    }
}
