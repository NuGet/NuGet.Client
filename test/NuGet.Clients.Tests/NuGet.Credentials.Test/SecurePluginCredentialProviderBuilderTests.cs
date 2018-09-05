// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;
using Xunit;

namespace NuGet.Credentials.Test
{
    public class SecurePluginCredentialProviderBuilderTests
    {
        [Fact]
        public void CredentialProviderBuilder_ThrowsExceptionForNullLogger()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProviderBuilder(CreateDefaultPluginManager(), true, null));
            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void CredentialProviderBuilder_ThrowsExceptionForNullPluginManager()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProviderBuilder(null, true, NullLogger.Instance));
            Assert.Equal("pluginManager", exception.ParamName);
        }

        [Fact]
        public async Task BuildAll_BuildsZero()
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecurePluginCredentialProviderBuilder(pluginManager.PluginManager, true, NullLogger.Instance);

            var credentialProviders = await builder.BuildAllAsync();
            Assert.Equal(0, credentialProviders.Count());
        }

        [Fact]
        public async Task BuildAll_BuildsPluginsInCorrectOrder()
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            plugins.Add(new KeyValuePair<string, PluginFileState>("a", PluginFileState.Valid));
            plugins.Add(new KeyValuePair<string, PluginFileState>("b", PluginFileState.Valid));
            plugins.Add(new KeyValuePair<string, PluginFileState>("c", PluginFileState.Valid));

            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecurePluginCredentialProviderBuilder(pluginManager.PluginManager, true, NullLogger.Instance);

            var credentialProviders = (await builder.BuildAllAsync()).ToArray();
            Assert.Equal(3, credentialProviders.Count());
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_a", credentialProviders[0].Id);
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_b", credentialProviders[1].Id);
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_c", credentialProviders[2].Id);
        }

        [Fact]
        public async Task BuildAll_BuildsAllPlugins()
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            plugins.Add(new KeyValuePair<string, PluginFileState>("a", PluginFileState.Valid));
            plugins.Add(new KeyValuePair<string, PluginFileState>("b", PluginFileState.InvalidEmbeddedSignature));
            plugins.Add(new KeyValuePair<string, PluginFileState>("c", PluginFileState.InvalidFilePath));
            plugins.Add(new KeyValuePair<string, PluginFileState>("d", PluginFileState.NotFound));


            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecurePluginCredentialProviderBuilder(pluginManager.PluginManager, true, NullLogger.Instance);

            var credentialProviders = (await builder.BuildAllAsync()).ToArray();
            Assert.Equal(4, credentialProviders.Count());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BuildAll_PassesCorrectCanShowDialogValue(bool canShowDialog)
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            plugins.Add(new KeyValuePair<string, PluginFileState>("a", PluginFileState.Valid));

            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecurePluginCredentialProviderBuilder(pluginManager.PluginManager, canShowDialog, NullLogger.Instance);

            var credentialProviders = (await builder.BuildAllAsync()).ToArray();
            Assert.Equal(1, credentialProviders.Count());
            var bla = typeof(SecurePluginCredentialProvider).GetTypeInfo().GetField("_canShowDialog", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            Assert.Equal(canShowDialog, bla.GetValue(credentialProviders.Single()));
        }

        private sealed class PluginManagerBuilderMock
        {
            internal PluginManager PluginManager { get; }
            internal PluginManagerBuilderMock(List<KeyValuePair<string, PluginFileState>> plugins)
            {
                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == CredentialTestConstants.PluginPathsEnvironmentVariable)))
                    .Returns(string.Join(";", plugins.Select(e => e.Key)));

                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == CredentialTestConstants.PluginRequestTimeoutEnvironmentVariable)))
                    .Returns("b");
                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == CredentialTestConstants.PluginIdleTimeoutEnvironmentVariable)))
                    .Returns("c");
                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == CredentialTestConstants.PluginHandshakeTimeoutEnvironmentVariable)))
                    .Returns("d");

                var pluginDiscoverer = new Mock<IPluginDiscoverer>(MockBehavior.Strict);
                var pluginDiscoveryResults = GetPluginDiscoveryResults(plugins);

                pluginDiscoverer.Setup(x => x.DiscoverAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(pluginDiscoveryResults);

                PluginManager = new PluginManager(
                    reader.Object,
                    new Lazy<IPluginDiscoverer>(() => pluginDiscoverer.Object),
                    (TimeSpan idleTimeout) => Mock.Of<IPluginFactory>());
            }

            private static IEnumerable<PluginDiscoveryResult> GetPluginDiscoveryResults(List<KeyValuePair<string, PluginFileState>> plugins)
            {
                var results = new List<PluginDiscoveryResult>();
                foreach (var plugin in plugins)
                {
                    var file = new PluginFile(plugin.Key, new Lazy<PluginFileState>(() => plugin.Value));
                    results.Add(new PluginDiscoveryResult(file));
                }

                return results;
            }
        }

        private static PluginManager CreateDefaultPluginManager()
        {
            return new PluginManager(
                reader: Mock.Of<IEnvironmentVariableReader>(),
                pluginDiscoverer: new Lazy<IPluginDiscoverer>(),
                pluginFactoryCreator: (TimeSpan idleTimeout) => Mock.Of<IPluginFactory>());
        }
    }
}
