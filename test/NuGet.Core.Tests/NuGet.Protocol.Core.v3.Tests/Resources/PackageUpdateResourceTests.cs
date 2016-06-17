using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class PackageUpdateResourceTests
    {
        private const string ApiKeyHeader = "X-NuGet-ApiKey";

        [Fact]
        public async Task PackageUpdateResource_IncludesApiKeyWhenDeleting()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var source = "https://www.nuget.org/api/v2";
                HttpRequestMessage actualRequest = null;
                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        "https://www.nuget.org/api/v2/DeepEqual/1.4.0.1-rc",
                        request =>
                        {
                            actualRequest = request;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
                var resource = await repo.GetResourceAsync<PackageUpdateResource>();
                var apiKey = "SomeApiKey";

                // Act
                await resource.Delete(
                    packageId: "DeepEqual",
                    packageVersion: "1.4.0.1-rc",
                    getApiKey: _ => apiKey,
                    confirm: _ => true,
                    log: NullLogger.Instance);

                // Assert
                Assert.NotNull(actualRequest);
                Assert.Equal(HttpMethod.Delete, actualRequest.Method);

                IEnumerable<string> values;
                actualRequest.Headers.TryGetValues(ApiKeyHeader, out values);
                Assert.Equal(1, values.Count());
                Assert.Equal(apiKey, values.First());

                Assert.False(
                    actualRequest.GetOrCreateConfiguration().PromptOn403,
                    "When the API key is provided, the user should not be prompted on HTTP 403.");
            }
        }

        [Fact]
        public async Task PackageUpdateResource_AllowsNoApiKeyWhenDeleting()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var source = "https://www.nuget.org/api/v2";
                HttpRequestMessage actualRequest = null;
                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        "https://www.nuget.org/api/v2/DeepEqual/1.4.0.1-rc",
                        request =>
                        {
                            actualRequest = request;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
                var resource = await repo.GetResourceAsync<PackageUpdateResource>();
                var apiKey = string.Empty;

                // Act
                await resource.Delete(
                    packageId: "DeepEqual",
                    packageVersion: "1.4.0.1-rc",
                    getApiKey: _ => apiKey,
                    confirm: _ => true,
                    log: NullLogger.Instance);

                // Assert
                Assert.NotNull(actualRequest);
                Assert.Equal(HttpMethod.Delete, actualRequest.Method);

                IEnumerable<string> values;
                actualRequest.Headers.TryGetValues(ApiKeyHeader, out values);
                Assert.Null(values);

                Assert.True(
                    actualRequest.GetOrCreateConfiguration().PromptOn403,
                    "When the API key is not provided, the user should be prompted on HTTP 403.");
            }
        }

        [Fact]
        public async Task PackageUpdateResource_AllowsApiKeyWhenPushing()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var source = "https://www.nuget.org/api/v2";
                HttpRequestMessage actualRequest = null;
                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        "https://www.nuget.org/api/v2/",
                        request =>
                        {
                            actualRequest = request;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
                var resource = await repo.GetResourceAsync<PackageUpdateResource>();
                var apiKey = "SomeApiKey";

                var packageInfo = SimpleTestPackageUtility.CreateFullPackage(workingDir, "test", "1.0.0");

                // Act
                await resource.Push(
                    packagePath: packageInfo.FullName,
                    symbolsSource: null,
                    timeoutInSecond: 5,
                    disableBuffering: false,
                    getApiKey: _ => apiKey,
                    log: NullLogger.Instance);

                // Assert
                Assert.NotNull(actualRequest);
                Assert.Equal(HttpMethod.Put, actualRequest.Method);

                IEnumerable<string> values;
                actualRequest.Headers.TryGetValues(ApiKeyHeader, out values);
                Assert.Equal(1, values.Count());
                Assert.Equal(apiKey, values.First());

                Assert.False(
                    actualRequest.GetOrCreateConfiguration().PromptOn403,
                    "When the API key is provided, the user should not be prompted on HTTP 403.");
            }
        }

        [Fact]
        public async Task PackageUpdateResource_AllowsNoApiKeyWhenPushing()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var source = "https://www.nuget.org/api/v2";
                HttpRequestMessage actualRequest = null;
                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        "https://www.nuget.org/api/v2/",
                        request =>
                        {
                            actualRequest = request;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
                var resource = await repo.GetResourceAsync<PackageUpdateResource>();
                var apiKey = string.Empty;

                var packageInfo = SimpleTestPackageUtility.CreateFullPackage(workingDir, "test", "1.0.0");

                // Act
                await resource.Push(
                    packagePath: packageInfo.FullName,
                    symbolsSource: null,
                    timeoutInSecond: 5,
                    disableBuffering: false,
                    getApiKey: _ => apiKey,
                    log: NullLogger.Instance);

                // Assert
                Assert.NotNull(actualRequest);
                Assert.Equal(HttpMethod.Put, actualRequest.Method);

                IEnumerable<string> values;
                actualRequest.Headers.TryGetValues(ApiKeyHeader, out values);
                Assert.Null(values);

                Assert.True(
                    actualRequest.GetOrCreateConfiguration().PromptOn403,
                    "When the API key is not provided, the user should be prompted on HTTP 403.");
            }
        }
    }
}
