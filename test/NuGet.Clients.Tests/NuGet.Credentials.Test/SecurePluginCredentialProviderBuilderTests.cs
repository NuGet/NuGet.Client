// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string _pluginHandshakeTimeoutEnvironmentVariable = "NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS";
        private const string _pluginIdleTimeoutEnvironmentVariable = "NUGET_PLUGIN_IDLE_TIMEOUT_IN_SECONDS";
        private const string _pluginPathsEnvironmentVariable = "NUGET_PLUGIN_PATHS";
        private const string _pluginRequestTimeoutEnvironmentVariable = "NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS";
        private const string _sourceUri = "https://unit.test";

        [Fact]
        public async Task BuildAll_BuildsZero()
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecureCredentialProviderBuilder(NullLogger.Instance);

            var credentialProviders = await builder.BuildAll();
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
            var builder = new SecureCredentialProviderBuilder(NullLogger.Instance);

            var credentialProviders = (await builder.BuildAll()).ToArray();
            Assert.Equal(3, credentialProviders.Count());
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_a", credentialProviders[0].Id);
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_b", credentialProviders[1].Id);
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_c", credentialProviders[2].Id);
        }

        [Fact]
        public async Task BuildAll_BuildsOnlyValidPlugins()
        {
            var plugins = new List<KeyValuePair<string, PluginFileState>>();
            plugins.Add(new KeyValuePair<string, PluginFileState>("a", PluginFileState.Valid));
            plugins.Add(new KeyValuePair<string, PluginFileState>("b", PluginFileState.InvalidEmbeddedSignature));
            plugins.Add(new KeyValuePair<string, PluginFileState>("c", PluginFileState.InvalidFilePath));
            plugins.Add(new KeyValuePair<string, PluginFileState>("d", PluginFileState.NotFound));


            var pluginManager = new PluginManagerBuilderMock(plugins);
            var builder = new SecureCredentialProviderBuilder(NullLogger.Instance);

            var credentialProviders = (await builder.BuildAll()).ToArray();
            Assert.Equal(1, credentialProviders.Count());
            Assert.StartsWith(nameof(SecurePluginCredentialProvider) + "_a", credentialProviders[0].Id);
        }

        private sealed class PluginManagerBuilderMock
        {

            internal PluginManagerBuilderMock(List<KeyValuePair<string, PluginFileState>> plugins)
            {
                var reader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginPathsEnvironmentVariable)))
                    .Returns(string.Join(";", plugins.Select(e => e.Key)));

                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginRequestTimeoutEnvironmentVariable)))
                    .Returns("b");
                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginIdleTimeoutEnvironmentVariable)))
                    .Returns("c");
                reader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == _pluginHandshakeTimeoutEnvironmentVariable)))
                    .Returns("d");

                var pluginDiscoverer = new Mock<IPluginDiscoverer>(MockBehavior.Strict);
                var pluginDiscoveryResults = GetPluginDiscoveryResults(plugins);

                pluginDiscoverer.Setup(x => x.DiscoverAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(pluginDiscoveryResults);

                PluginManager.Instance.Reinitialize(
                    reader.Object,
                    new Lazy<IPluginDiscoverer>(() => pluginDiscoverer.Object),
                    (TimeSpan idleTimeout) => Mock.Of<IPluginFactory>());
            }

            private static IEnumerable<PluginDiscoveryResult> GetPluginDiscoveryResults(List<KeyValuePair<string, PluginFileState>> plugins)
            {
                var results = new List<PluginDiscoveryResult>();
                foreach (var plugin in plugins)
                {
                    var file = new PluginFile(plugin.Key, plugin.Value);
                    results.Add(new PluginDiscoveryResult(file));
                }

                return results;
            }
        }

    }
}
