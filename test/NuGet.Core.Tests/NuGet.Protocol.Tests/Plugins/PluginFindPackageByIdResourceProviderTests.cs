// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginFindPackageByIdResourceProviderTests
    {
        private readonly PluginFindPackageByIdResourceProvider _provider;

        public PluginFindPackageByIdResourceProviderTests()
        {
            _provider = new PluginFindPackageByIdResourceProvider();
        }

        [Fact]
        public void Before_IsBeforeHttpFileSystemBasedFindPackageByIdResourceProvider()
        {
            Assert.Equal(new[] { nameof(HttpFileSystemBasedFindPackageByIdResourceProvider) }, _provider.Before);
        }

        [Fact]
        public void After_IsEmpty()
        {
            Assert.Empty(_provider.After);
        }

        [Fact]
        public void Name_IsPluginFindPackageByIdResourceProvider()
        {
            Assert.Equal(nameof(PluginFindPackageByIdResourceProvider), _provider.Name);
        }

        [Fact]
        public void ResourceType_IsFindPackageByIdResource()
        {
            Assert.Equal(typeof(FindPackageByIdResource), _provider.ResourceType);
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
            var sourceRepository = CreateSourceRepository(createPluginResource: true);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _provider.TryCreate(sourceRepository, new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task TryCreate_ReturnsFalseIfNoPluginResource()
        {
            var sourceRepository = CreateSourceRepository(createPluginResource: false);

            var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task TryCreate_ReturnsPluginFindPackageByIdResource()
        {
            var sourceRepository = CreateSourceRepository(createPluginResource: true);

            var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.True(result.Item1);
            Assert.IsType<PluginFindPackageByIdResource>(result.Item2);
        }

        private static SourceRepository CreateSourceRepository(bool createPluginResource)
        {
            var packageSource = new PackageSource(source: "");
            var provider = new Mock<PluginResourceProvider>();

            provider.Setup(x => x.Name)
                .Returns(nameof(PluginResourceProvider));
            provider.Setup(x => x.ResourceType)
                .Returns(typeof(PluginResource));

            var pluginResource = createPluginResource ? new PluginResource(Enumerable.Empty<PluginCreationResult>()) : null;
            var tryCreateResult = new Tuple<bool, INuGetResource>(pluginResource != null, pluginResource);

            provider.Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tryCreateResult));

            return new SourceRepository(packageSource, new[] { provider.Object });
        }
    }
}