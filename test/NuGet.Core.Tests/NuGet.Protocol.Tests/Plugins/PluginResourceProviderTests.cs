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
        private const string _environmentVariable = "NUGET_PLUGIN_PATHS";
        private readonly PluginResourceProvider _provider;

        public PluginResourceProviderTests()
        {
            _provider = new PluginResourceProvider();
        }

        [Fact]
        public void EnvironmentVariableReader_IsEnvironmentVariableWrapper()
        {
            Assert.IsType<EnvironmentVariableWrapper>(PluginResourceProvider.EnvironmentVariableReader);
        }

        [Fact]
        public void Before_IsEmpty()
        {
            Assert.Empty(_provider.Before);
        }

        [Fact]
        public void After_IsEmpty()
        {
            Assert.Empty(_provider.After);
        }

        [Fact]
        public void Name_IsPluginResourceProvider()
        {
            Assert.Equal(nameof(PluginResourceProvider), _provider.Name);
        }

        [Fact]
        public void ResourceType_IsPluginResource()
        {
            Assert.Equal(typeof(PluginResource), _provider.ResourceType);
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
            var sourceRepository = CreateSourceRepository(serviceIndexResource: null);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _provider.TryCreate(sourceRepository, new CancellationToken(canceled: true)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task TryCreate_ReturnsFalseForNullOrEmptyEnvironmentVariable(string pluginsPath)
        {
            var serviceIndex = new ServiceIndexResourceV3(JObject.Parse("{}"), DateTime.UtcNow);
            var sourceRepository = CreateSourceRepository(serviceIndex);

            using (new EnvironmentVariableReaderResetter())
            {
                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(It.Is<string>(value => value == _environmentVariable)))
                    .Returns(pluginsPath);

                PluginResourceProvider.EnvironmentVariableReader = reader.Object;

                var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

                Assert.False(result.Item1);
                Assert.Null(result.Item2);
            }
        }

        [Fact]
        public async Task TryCreate_ReturnsFalseIfNoServiceIndexResourceV3()
        {
            var sourceRepository = CreateSourceRepository(serviceIndexResource: null);

            using (new EnvironmentVariableReaderResetter())
            {
                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(It.Is<string>(value => value == _environmentVariable)))
                    .Returns("a");

                PluginResourceProvider.EnvironmentVariableReader = reader.Object;

                var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

                Assert.False(result.Item1);
                Assert.Null(result.Item2);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task TryCreate_ReturnsPluginResource()
        {
            var serviceIndex = new ServiceIndexResourceV3(JObject.Parse("{}"), DateTime.UtcNow);
            var sourceRepository = CreateSourceRepository(serviceIndex);

            using (new EnvironmentVariableReaderResetter())
            {
                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(It.Is<string>(value => value == _environmentVariable)))
                    .Returns("a");

                PluginResourceProvider.EnvironmentVariableReader = reader.Object;

                var result = await _provider.TryCreate(sourceRepository, CancellationToken.None);

                Assert.True(result.Item1);
                Assert.IsType<PluginResource>(result.Item2);
            }
        }

        private static SourceRepository CreateSourceRepository(ServiceIndexResourceV3 serviceIndexResource)
        {
            var packageSource = new PackageSource(source: "a");
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

        private sealed class EnvironmentVariableReaderResetter : IDisposable
        {
            private readonly IEnvironmentVariableReader _originalReader;

            internal EnvironmentVariableReaderResetter()
            {
                _originalReader = PluginResourceProvider.EnvironmentVariableReader;
            }

            public void Dispose()
            {
                PluginResourceProvider.EnvironmentVariableReader = _originalReader;
            }
        }
    }
}