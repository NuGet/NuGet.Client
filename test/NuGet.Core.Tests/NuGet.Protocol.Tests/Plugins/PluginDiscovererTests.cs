// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginDiscovererTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_AcceptsAnyString(string rawPluginPaths)
        {
            using (new PluginDiscoverer(rawPluginPaths))
            {
            }
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsIfCancelled()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: ""))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => discoverer.DiscoverAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task DiscoverAsync_DoesNotThrowIfNoValidFilePathsAndFallbackEmbeddedSignatureVerifier()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: ";"))
            {
                var pluginFiles = await discoverer.DiscoverAsync(CancellationToken.None);

                Assert.Empty(pluginFiles);
            }
        }

        [Fact]
        public async Task DiscoverAsync_PerformsDiscoveryOnlyOnce()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                var responses = new Dictionary<string, bool>()
                {
                    { pluginPath, true }
                };

                using (var discoverer = new PluginDiscoverer(pluginPath))
                {
                    var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    foreach (var result in results)
                    {
                        var pluginState = result.PluginFile.State.Value;
                    }

                    Assert.Equal(1, results.Length);

                    results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(1, results.Length);
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_HandlesAllPluginFileStates()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPaths = new[] { "a", "b", }
                    .Select(fileName => Path.Combine(testDirectory.Path, fileName))
                    .ToArray();

                File.WriteAllText(pluginPaths[1], string.Empty);

                string rawPluginPaths =
                    $"{pluginPaths[0]};{pluginPaths[1]};c";

                using (var discoverer = new PluginDiscoverer(rawPluginPaths))
                {
                    var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(3, results.Length);

                    Assert.Equal(pluginPaths[0], results[0].PluginFile.Path);
                    Assert.Equal(PluginFileState.NotFound, results[0].PluginFile.State.Value);
                    Assert.Equal($"A plugin was not found at path '{pluginPaths[0]}'.", results[0].Message);

                    Assert.Equal(pluginPaths[1], results[1].PluginFile.Path);
                    Assert.Equal(PluginFileState.Valid, results[1].PluginFile.State.Value);
                    Assert.Null(results[1].Message);

                    Assert.Equal("c", results[2].PluginFile.Path);
                    Assert.Equal(PluginFileState.InvalidFilePath, results[2].PluginFile.State.Value);
                    Assert.Equal($"The plugin file path 'c' is invalid.", results[2].Message);
                }
            }
        }

        [Theory]
        [InlineData("a")]
        [InlineData(@"\a")]
        [InlineData(@".\a")]
        [InlineData(@"..\a")]
        public async Task DiscoverAsync_DisallowsNonRootedFilePaths(string pluginPath)
        {
            var responses = new Dictionary<string, bool>() { { pluginPath, true } };

            using (var discoverer = new PluginDiscoverer(pluginPath))
            {
                var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, results.Length);
                Assert.Equal(pluginPath, results[0].PluginFile.Path);
                Assert.Equal(PluginFileState.InvalidFilePath, results[0].PluginFile.State.Value);
            }
        }

        [Fact]
        public async Task DiscoverAsync_IsIdempotent()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                using (var discoverer = new PluginDiscoverer(pluginPath))
                {
                    var firstResult = await discoverer.DiscoverAsync(CancellationToken.None);
                    var firstState = firstResult.SingleOrDefault().PluginFile.State.Value;

                    var secondResult = await discoverer.DiscoverAsync(CancellationToken.None);
                    var secondState = secondResult.SingleOrDefault().PluginFile.State.Value;

                    Assert.Same(firstResult, secondResult);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("nuget-plugin-myPlugin.exe")]
        [InlineData("nuget-plugin-myPlugin.bat")]
        public async Task DiscoverAsync_withValidDotNetToolsPluginWindows_FindsThePlugin(string fileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugin");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, fileName);

                Environment.SetEnvironmentVariable("PATH", pluginPath);
                File.WriteAllText(myPlugin, string.Empty);
                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer("", verifierSpy.Object))
                {
                    // Act
                    var result = await discoverer.DiscoverAsync(CancellationToken.None);

                    // Assert
                    var discovered = false;

                    foreach (PluginDiscoveryResult discoveryResult in result)
                    {
                        if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                    }

                    Assert.True(discovered);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("nugetplugin-myPlugin.exe")]
        [InlineData("nugetplugin-myPlugin.bat")]
        public async Task DiscoverAsync_withInValidDotNetToolsPluginNameWindows_DoesNotFindThePlugin(string fileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugin");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, fileName);

                Environment.SetEnvironmentVariable("PATH", pluginPath);
                File.WriteAllText(myPlugin, string.Empty);
                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer("", verifierSpy.Object))
                {
                    // Act
                    var result = await discoverer.DiscoverAsync(CancellationToken.None);

                    // Assert
                    var discovered = false;

                    foreach (PluginDiscoveryResult discoveryResult in result)
                    {
                        if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                    }

                    Assert.True(discovered);
                }
            }
        }

        [PlatformFact(Platform.Linux)]
        public async Task DiscoverAsync_withValidDotNetToolsPluginLinux_FindsThePlugin()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugins");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, "nuget-plugin-MyPlugin");

                using (var process = new Process())
                {
                    // Use a shell command to make the file executable
                    process.StartInfo.FileName = "sh";
                    process.StartInfo.Arguments = $"-c \"chmod +x {myPlugin}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    if (!process.WaitForExit(1000) || process.ExitCode != 0)
                    {
                        Environment.SetEnvironmentVariable("PATH", pluginPath);
                        File.WriteAllText(myPlugin, string.Empty);
                        var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                        verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                            .Returns(true);

                        using (var discoverer = new PluginDiscoverer("", verifierSpy.Object))
                        {
                            // Act
                            var result = await discoverer.DiscoverAsync(CancellationToken.None);

                            // Assert
                            var discovered = false;

                            foreach (PluginDiscoveryResult discoveryResult in result)
                            {
                                if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                            }

                            Assert.True(discovered);
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Linux)]
        public async Task DiscoverAsync_withNoExecutableValidDotNetToolsPluginLinux_DoesNotFindThePlugin()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugins");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, "nuget-plugin-MyPlugin");

                using (var process = new Process())
                {
                    // Use a shell command to make the file executable
                    process.StartInfo.FileName = "sh";
                    process.StartInfo.Arguments = $"-c \"chmod -x {myPlugin}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    if (!process.WaitForExit(1000) || process.ExitCode != 0)
                    {
                        Environment.SetEnvironmentVariable("PATH", pluginPath);
                        File.WriteAllText(myPlugin, string.Empty);
                        var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                        verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                            .Returns(true);

                        using (var discoverer = new PluginDiscoverer("", verifierSpy.Object))
                        {
                            // Act
                            var result = await discoverer.DiscoverAsync(CancellationToken.None);

                            // Assert
                            var discovered = false;

                            foreach (PluginDiscoveryResult discoveryResult in result)
                            {
                                if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                            }

                            Assert.False(discovered);
                        }
                    }
                }
            }
        }

        private sealed class EmbeddedSignatureVerifierStub : EmbeddedSignatureVerifier
        {
            private readonly Dictionary<string, bool> _responses;

            internal int IsValidCallCount { get; private set; }

            public EmbeddedSignatureVerifierStub(Dictionary<string, bool> responses)
            {
                _responses = responses;
            }

            public override bool IsValid(string filePath)
            {
                ++IsValidCallCount;

                bool value;

                Assert.True(_responses.TryGetValue(filePath, out value));

                return value;
            }
        }
    }
}
