using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class HttpSourceTests
    {
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
                var source = "https://fake.server/users.json";
                CacheValidationException = new Exception();
                NetworkValidationException = new Exception();
                CacheContent = "cache";
                NetworkContent = "network";
                CacheKey = "CacheKey";
                Url = "https://fake.server/foo/bar/something.json";

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
