// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Tests.Utility;
using NuGet.Test.Server;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class HttpSourceTests
    {
        /// <summary>
        /// We need a lock whenever we set static properties on <see cref="HttpHandlerResourceV3"/>.
        /// </summary>
        private static readonly SemaphoreSlim HttpHandlerResourceV3Lock = new SemaphoreSlim(1);
        private const string FakeSource = "https://fake.server/users.json";

        [Fact]
        public async Task HttpSource_PromptsForCredentialsOn401()
        {
            // Arrange
            using (await UsingSemaphore.WaitAsync(HttpHandlerResourceV3Lock))
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);

                tc.SetResponseSequence(new[]
                {
                    new HttpResponseMessage(HttpStatusCode.Unauthorized),
                    new HttpResponseMessage(HttpStatusCode.OK),
                });

                var prompted = false;
                HttpHandlerResourceV3.PromptForCredentialsAsync = (uri, type, message, token) =>
                {
                    prompted = true;
                    return Task.FromResult(tc.Credentials);
                };

                // Act
                var statusCode = await tc.HttpSource.ProcessResponseAsync(
                    () => new HttpRequestMessage(),
                    response =>
                    {
                        return Task.FromResult(response.StatusCode);
                    },
                    tc.Logger,
                    CancellationToken.None);

                // Assert
                Assert.True(prompted, "The user should have been prompted for credentials.");
                Assert.Equal(HttpStatusCode.OK, statusCode);
            }
        }

        [Fact]
        public async Task HttpSource_PromptsForCredentialsOn403()
        {
            // Arrange
            using (await UsingSemaphore.WaitAsync(HttpHandlerResourceV3Lock))
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);

                tc.SetResponseSequence(new[]
                {
                    new HttpResponseMessage(HttpStatusCode.Forbidden),
                    new HttpResponseMessage(HttpStatusCode.OK),
                });

                var prompted = false;
                HttpHandlerResourceV3.PromptForCredentialsAsync = (uri, type, message, token) =>
                {
                    prompted = true;
                    return Task.FromResult(tc.Credentials);
                };

                // Act
                var statusCode = await tc.HttpSource.ProcessResponseAsync(
                    () => new HttpRequestMessage(),
                    response =>
                    {
                        return Task.FromResult(response.StatusCode);
                    },
                    tc.Logger,
                    CancellationToken.None);

                // Assert
                Assert.True(prompted, "The user should have been prompted for credentials.");
                Assert.Equal(HttpStatusCode.OK, statusCode);
            }
        }

        [Fact]
        public async Task HttpSource_GetNoContent()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);

                tc.SetResponseSequence(new[]
                {
                    new HttpResponseMessage(HttpStatusCode.NoContent),
                });

                // Act
                var result = await tc.HttpSource.GetAsync(
                    tc.Url,
                    tc.CacheKey,
                    tc.CacheContext,
                    tc.Logger,
                    ignoreNotFounds: false,
                    ensureValidContents: tc.GetStreamValidator(validCache: true, validNetwork: true),
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(HttpSourceResultStatus.NoContent, result.Status);
            }
        }

        [Fact]
        public void HttpSource_DefaultDownloadTimeout()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);
                var expected = TimeSpan.FromSeconds(60);

                // Act
                var actual = tc.HttpSource.DownloadTimeout;

                // Assert
                Assert.Equal(expected, actual);
            }
        }
        
        [Fact]
        public async Task HttpSource_TimesOutDownload()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var expectedMilliseconds = 250;
                var server = new TcpListenerServer
                {
                    Mode = TestServerMode.SlowResponseBody,
                    SleepDuration = TimeSpan.FromSeconds(10)
                };
                var packageSource = new PackageSource(FakeSource);
                var handler = new HttpClientHandler();
                var handlerResource = new HttpHandlerResourceV3(handler, handler);
                var httpSource = new HttpSource(packageSource, () => Task.FromResult((HttpHandlerResource)handlerResource))
                {
                    HttpCacheDirectory = td,
                    DownloadTimeout = TimeSpan.FromMilliseconds(expectedMilliseconds)
                };
            
                // Act & Assert
                var actual = await Assert.ThrowsAsync<IOException>(() => 
                    server.ExecuteAsync(uri => httpSource.GetJObjectAsync(
                        new Uri(uri),
                        false,
                        new TestLogger(),
                        CancellationToken.None)));
                Assert.IsType<TimeoutException>(actual.InnerException);
                Assert.EndsWith(
                    $"timed out because no data was received for {expectedMilliseconds}ms.",
                    actual.Message);
            }
        }
        
        [Fact]
        public async Task HttpSource_ValidatesValidNetworkContent()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);

                // Act
                var result = await tc.HttpSource.GetAsync(
                    tc.Url,
                    tc.CacheKey,
                    tc.CacheContext,
                    tc.Logger,
                    ignoreNotFounds: false,
                    ensureValidContents: tc.GetStreamValidator(validCache: true, validNetwork: true),
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.False(tc.ValidatedCacheContent, "The cache content should not have been cached at all.");
                Assert.True(tc.ValidatedNetworkContent, "The network content should have been validated.");
                Assert.Equal(tc.NetworkContent, tc.ReadStream(result.Stream));
            }
        }

        [Fact]
        public async Task HttpSource_ValidatesInvalidNetworkContent()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<Exception>(() => tc.HttpSource.GetAsync(
                    tc.Url,
                    tc.CacheKey,
                    tc.CacheContext,
                    tc.Logger,
                    ignoreNotFounds: false,
                    ensureValidContents: tc.GetStreamValidator(validCache: true, validNetwork: false),
                    cancellationToken: CancellationToken.None));

                // Assert
                Assert.Same(tc.NetworkValidationException, exception);
                Assert.False(tc.ValidatedCacheContent, "The cache content should not have been cached at all.");
                Assert.True(tc.ValidatedNetworkContent, "The network content should have been validated.");
            }
        }

        [Fact]
        public async Task HttpSource_ValidatesValidCachedContent()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);
                tc.WriteToCache(tc.CacheKey, tc.CacheContent);

                // Act
                var result = await tc.HttpSource.GetAsync(
                    tc.Url,
                    tc.CacheKey,
                    tc.CacheContext,
                    tc.Logger,
                    ignoreNotFounds: false,
                    ensureValidContents: tc.GetStreamValidator(validCache: true, validNetwork: true),
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.True(tc.ValidatedCacheContent, "The cache content should have been validated.");
                Assert.False(tc.ValidatedNetworkContent, "The network should not have been queried at all.");
                Assert.Equal(tc.CacheContent, tc.ReadStream(result.Stream));
            }
        }

        [Fact]
        public async Task HttpSource_ValidatesInvalidCachedContent()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(td);
                tc.WriteToCache(tc.CacheKey, tc.CacheContent);

                // Act
                var result = await tc.HttpSource.GetAsync(
                    tc.Url,
                    tc.CacheKey,
                    tc.CacheContext,
                    tc.Logger,
                    ignoreNotFounds: false,
                    ensureValidContents: tc.GetStreamValidator(validCache: false, validNetwork: true),
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.True(tc.ValidatedCacheContent, "The cache content should have been validated.");
                Assert.True(tc.ValidatedNetworkContent, "The network content should have been validated.");
                Assert.Equal(tc.NetworkContent, new StreamReader(result.Stream).ReadToEnd());
            }
        }

        private class TestContext
        {
            private readonly string _cacheDirectory;

            public TestContext(TestDirectory testDirectory)
            {
                // data
                var source = FakeSource;
                CacheValidationException = new Exception();
                NetworkValidationException = new Exception();
                CacheContent = "cache";
                NetworkContent = "network";
                CacheKey = "CacheKey";
                Url = "https://fake.server/foo/bar/something.json";
                Credentials = new NetworkCredential("foo", "bar");

                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    _cacheDirectory = "c810bdb33f8c56015efcaf435f94766aa0c4748c$https:_fake.server_users.json";
                }
                else
                {
                    // colon is not valid path character on Windows
                    _cacheDirectory = "c810bdb33f8c56015efcaf435f94766aa0c4748c$https_fake.server_users.json";
                }

                // dependencies
                var packageSource = new PackageSource(source);
                var networkResponses = new Dictionary<string, string> { { Url, NetworkContent } };
                var messageHandler = new TestMessageHandler(networkResponses, string.Empty);
                var handlerResource = new TestHttpHandler(messageHandler);
                CacheContext = new HttpSourceCacheContext();
                Logger = new TestLogger();
                TestDirectory = testDirectory;
                RetryHandlerMock = new Mock<IHttpRetryHandler>();

                // target
                HttpSource = new HttpSource(packageSource, () => Task.FromResult((HttpHandlerResource)handlerResource))
                {
                    HttpCacheDirectory = TestDirectory
                };
            }

            public Exception CacheValidationException { get; }

            public TestDirectory TestDirectory { get; }

            public string Url { get; }

            public string CacheKey { get; }

            public string NetworkContent { get; }

            public string CacheContent { get; }

            public TestLogger Logger { get; }

            public HttpSourceCacheContext CacheContext { get; }

            public HttpSource HttpSource { get; }

            public Exception NetworkValidationException { get; }

            public bool ValidatedNetworkContent { get; set; }

            public bool ValidatedCacheContent { get; set; }
            public Mock<IHttpRetryHandler> RetryHandlerMock { get; }
            public ICredentials Credentials { get; }

            public void WriteToCache(string cacheKey, string content)
            {
                var directory = Path.Combine(TestDirectory, _cacheDirectory);
                Directory.CreateDirectory(directory);

                var path = Path.Combine(directory, cacheKey + ".dat");
                File.WriteAllText(path, content, new UTF8Encoding(false));
            }

            public string ReadStream(Stream stream)
            {
                return new StreamReader(stream, Encoding.UTF8).ReadToEnd();
            }

            public void SetResponseSequence(HttpResponseMessage[] responses)
            {
                HttpSource.RetryHandler = RetryHandlerMock.Object;
                int index = 0;
                RetryHandlerMock
                    .Setup(x => x.SendAsync(
                        It.IsAny<HttpClient>(),
                        It.IsAny<Func<HttpRequestMessage>>(),
                        It.IsAny<HttpCompletionOption>(),
                        It.IsAny<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult(responses[index++ % responses.Length]));
            }

            public Action<Stream> GetStreamValidator(bool validCache, bool validNetwork)
            {
                return stream =>
                {
                    var content = ReadStream(stream);

                    if (content == CacheContent)
                    {
                        ValidatedCacheContent = true;
                        if (!validCache)
                        {
                            throw CacheValidationException;
                        }
                    }

                    if (content == NetworkContent)
                    {
                        ValidatedNetworkContent = true;
                        if (!validNetwork)
                        {
                            throw NetworkValidationException;
                        }
                    }
                };
            }
        }
    }
}
