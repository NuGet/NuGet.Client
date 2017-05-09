// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginResourceProviderTests
    {
        private const string _pluginPathsEnvironmentVariable = "NUGET_PLUGIN_PATHS";
        private const string _pluginRequestTimeoutEnvironmentVariable = "NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS";
        private const string _sourceUri = "https://unit.test";

        [Fact]
        public void Before_IsEmpty()
        {
            var provider = new PluginResourceProvider();

            Assert.Empty(provider.Before);
        }

        [Fact]
        public void After_IsEmpty()
        {
            var provider = new PluginResourceProvider();

            Assert.Empty(provider.After);
        }

        [Fact]
        public void Name_IsPluginResourceProvider()
        {
            var provider = new PluginResourceProvider();

            Assert.Equal(nameof(PluginResourceProvider), provider.Name);
        }

        [Fact]
        public void ResourceType_IsPluginResource()
        {
            var provider = new PluginResourceProvider();

            Assert.Equal(typeof(PluginResource), provider.ResourceType);
        }

        [Fact]
        public async Task TryCreate_ThrowsForNullSource()
        {
            var provider = new PluginResourceProvider();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => provider.TryCreate(source: null, cancellationToken: CancellationToken.None));

            Assert.Equal("source", exception.ParamName);
        }

        [Fact]
        public async Task TryCreate_ThrowsIfCancelled()
        {
            var sourceRepository = CreateSourceRepository(serviceIndexResource: null, sourceUri: _sourceUri);
            var provider = new PluginResourceProvider();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => provider.TryCreate(sourceRepository, new CancellationToken(canceled: true)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task TryCreate_ReturnsFalseForNullOrEmptyEnvironmentVariable(string pluginsPath)
        {
            var test = new PluginResourceProviderTest(
                serviceIndexJson: "{}",
                sourceUri: _sourceUri,
                pluginsPath: pluginsPath);

            var result = await test.Provider.TryCreate(test.SourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task TryCreate_ReturnsFalseIfNoServiceIndexResourceV3()
        {
            var test = new PluginResourceProviderTest(
                serviceIndexJson: null,
                sourceUri: _sourceUri);

            var result = await test.Provider.TryCreate(test.SourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Theory]
        [InlineData("\\unit\test")]
        [InlineData("file:///C:/unit/test")]
        public async Task TryCreate_ReturnsFalseIfPackageSourceIsNotHttpOrHttps(string sourceUri)
        {
            var test = new PluginResourceProviderTest(
                serviceIndexJson: "{}",
                sourceUri: sourceUri);

            var result = await test.Provider.TryCreate(test.SourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [PlatformFact(Platform.Windows)]
        public async Task TryCreate_ReturnsPluginResource()
        {
            var test = new PluginResourceProviderTest(
                serviceIndexJson: "{}",
                sourceUri: _sourceUri);

            var result = await test.Provider.TryCreate(test.SourceRepository, CancellationToken.None);

            Assert.True(result.Item1);
            Assert.IsType<PluginResource>(result.Item2);
        }

        [Fact]
        public void Reinitialize_ThrowsForNullReader()
        {
            var provider = new PluginResourceProvider();
            var exception = Assert.Throws<ArgumentNullException>(
                () => provider.Reinitialize(reader: null));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public void Reinitialize_SetsNewReader()
        {
            var reader = Mock.Of<IEnvironmentVariableReader>();
            var provider = new PluginResourceProvider();

            provider.Reinitialize(reader);

            Assert.Same(reader, PluginResourceProvider.EnvironmentVariableReader);
        }

        private sealed class PluginResourceProviderTest
        {
            internal PluginResourceProvider Provider { get; }
            internal SourceRepository SourceRepository { get; }

            internal PluginResourceProviderTest(string serviceIndexJson, string sourceUri, string pluginsPath = "a")
            {
                var serviceIndex = string.IsNullOrEmpty(serviceIndexJson)
                    ? null : new ServiceIndexResourceV3(JObject.Parse(serviceIndexJson), DateTime.UtcNow);

                SourceRepository = CreateSourceRepository(serviceIndex, sourceUri);

                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginPathsEnvironmentVariable)))
                    .Returns(pluginsPath);
                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginRequestTimeoutEnvironmentVariable)))
                    .Returns("b");

                Provider = new PluginResourceProvider();

                Provider.Reinitialize(reader.Object);
            }
        }

        private static SourceRepository CreateSourceRepository(
            ServiceIndexResourceV3 serviceIndexResource,
            string sourceUri)
        {
            var packageSource = new PackageSource(sourceUri);
            var provider = new Mock<ServiceIndexResourceV3Provider>();

            provider.Setup(x => x.Name)
                .Returns(nameof(ServiceIndexResourceV3Provider));
            provider.Setup(x => x.ResourceType)
                .Returns(typeof(ServiceIndexResourceV3));

            var tryCreateResult = new Tuple<bool, INuGetResource>(serviceIndexResource != null, serviceIndexResource);

            provider.Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tryCreateResult));

            return new SourceRepository(packageSource, new[] { provider.Object });
        }
    }
}