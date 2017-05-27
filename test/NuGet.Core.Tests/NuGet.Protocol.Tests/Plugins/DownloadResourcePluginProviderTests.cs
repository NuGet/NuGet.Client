// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class DownloadResourcePluginProviderTests
    {
        private readonly DownloadResourcePluginProvider _provider;

        private static readonly PluginResource _pluginResource = new PluginResource(Enumerable.Empty<PluginCreationResult>());

        public DownloadResourcePluginProviderTests()
        {
            _provider = new DownloadResourcePluginProvider();
        }

        [Fact]
        public void Before_IsBeforeDownloadResourceV3Provider()
        {
            Assert.Equal(new[] { nameof(DownloadResourceV3Provider) }, _provider.Before);
        }

        [Fact]
        public void After_IsEmpty()
        {
            Assert.Empty(_provider.After);
        }

        [Fact]
        public void Name_IsDownloadResourceV3PluginProvider()
        {
            Assert.Equal(nameof(DownloadResourcePluginProvider), _provider.Name);
        }

        [Fact]
        public void ResourceType_IsDownloadResource()
        {
            Assert.Equal(typeof(DownloadResource), _provider.ResourceType);
        }

        [Fact]
        public async Task TryCreate_ThrowsForNullSource()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _provider.TryCreate(source: null, cancellationToken: CancellationToken.None));

            Assert.Equal("source", exception.ParamName);
        }

        [Fact]
        public async Task TryCreate_ThrowsIfCancelled()
        {
            var sourceRepository = CreateSourceRepository(
                createPluginResource: true,
                createServiceIndexResourceV3: true);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _provider.TryCreate(sourceRepository, new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task TryCreate_ReturnsFalseIfNoPluginResource()
        {
            var sourceRepository = CreateSourceRepository(
                createPluginResource: false,
                createServiceIndexResourceV3: true);

            var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task TryCreate_ReturnsFalseIfNoServiceIndexResourceV3()
        {
            var sourceRepository = CreateSourceRepository(
                createPluginResource: true,
                createServiceIndexResourceV3: false);

            var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task TryCreate_ReturnsDownloadResourceV3Plugin()
        {
            var sourceRepository = CreateSourceRepository(
                createPluginResource: true,
                createServiceIndexResourceV3: true);

            var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.True(result.Item1);
            Assert.IsType<DownloadResourcePlugin>(result.Item2);
        }

        private static SourceRepository CreateSourceRepository(bool createPluginResource, bool createServiceIndexResourceV3)
        {
            var packageSource = new PackageSource(source: "");
            var providers = new INuGetResourceProvider[]
            {
                CreatePluginResourceProvider(createPluginResource),
                CreateServiceIndexResourceV3Provider(createServiceIndexResourceV3)
            };

            return new SourceRepository(packageSource, providers);
        }

        private static PluginResourceProvider CreatePluginResourceProvider(bool createResource)
        {
            var provider = new Mock<PluginResourceProvider>();

            provider.Setup(x => x.Name)
                .Returns(nameof(PluginResourceProvider));
            provider.Setup(x => x.ResourceType)
                .Returns(typeof(PluginResource));

            var pluginResource = createResource
                ? new PluginResource(Enumerable.Empty<PluginCreationResult>()) : null;
            var tryCreateResult = new Tuple<bool, INuGetResource>(pluginResource != null, pluginResource);

            provider.Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tryCreateResult));

            return provider.Object;
        }

        private static ServiceIndexResourceV3Provider CreateServiceIndexResourceV3Provider(bool createResource)
        {
            var provider = new Mock<ServiceIndexResourceV3Provider>();

            provider.Setup(x => x.Name)
                .Returns(nameof(ServiceIndexResourceV3Provider));
            provider.Setup(x => x.ResourceType)
                .Returns(typeof(ServiceIndexResourceV3));

            var serviceIndexResource = createResource
                ? new ServiceIndexResourceV3(JObject.Parse("{}"), DateTime.UtcNow) : null;
            var tryCreateResult = new Tuple<bool, INuGetResource>(serviceIndexResource != null, serviceIndexResource);

            provider.Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tryCreateResult));

            return provider.Object;
        }
    }
}