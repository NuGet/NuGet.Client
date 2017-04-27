// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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
            using (new PluginDiscoverer(rawPluginPaths, Mock.Of<EmbeddedSignatureVerifier>()))
            {
            }
        }

        [Fact]
        public void DiscoverAsync_ThrowsForNullVerifier()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginDiscoverer(rawPluginPaths: "", verifier: null));

            Assert.Equal("verifier", exception.ParamName);
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsIfCancelled()
        {
            using (var discoverer = new PluginDiscoverer(
                rawPluginPaths: "",
                verifier: Mock.Of<EmbeddedSignatureVerifier>()))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => discoverer.DiscoverAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsPlatformNotSupportedIfEmbeddedSignatureVerifierIsRequired()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                using (var discoverer = new PluginDiscoverer(pluginPath, new FallbackEmbeddedSignatureVerifier()))
                {
                    await Assert.ThrowsAsync<PlatformNotSupportedException>(
                        () => discoverer.DiscoverAsync(CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_DoesNotThrowIfNoValidFilePathsAndFallbackEmbeddedSignatureVerifier()
        {
            using (var discoverer = new PluginDiscoverer(
                rawPluginPaths: "",
                verifier: new FallbackEmbeddedSignatureVerifier()))
            {
                var pluginFiles = await discoverer.DiscoverAsync(CancellationToken.None);

                Assert.Empty(pluginFiles);
            }
        }

        [Fact]
        public async Task DiscoverAsync_HandlesAllPluginFileStates()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPaths = new[] { "b", "c" }
                    .Select(fileName => Path.Combine(testDirectory.Path, fileName))
                    .ToArray();

                foreach (var pluginPath in pluginPaths)
                {
                    File.WriteAllText(pluginPath, string.Empty);
                }

                var responses = new Dictionary<string, bool>()
                {
                    { "a", false },
                    { pluginPaths[0], false },
                    { pluginPaths[1], true },
                    { "d", false },
                };
                var verifierStub = new EmbeddedSignatureVerifierStub(responses);
                var rawPluginPaths = string.Join(";", responses.Keys);

                using (var discoverer = new PluginDiscoverer(rawPluginPaths, verifierStub))
                {
                    var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(4, results.Length);

                    Assert.Equal("a", results[0].PluginFile.Path);
                    Assert.Equal(PluginFileState.NotFound, results[0].PluginFile.State);
                    Assert.Equal("A plugin was not found at path 'a'.", results[0].Message);

                    Assert.Equal(pluginPaths[0], results[1].PluginFile.Path);
                    Assert.Equal(PluginFileState.InvalidEmbeddedSignature, results[1].PluginFile.State);
                    Assert.Equal($"The plugin at '{pluginPaths[0]}' did not have a valid embedded signature.", results[1].Message);

                    Assert.Equal(pluginPaths[1], results[2].PluginFile.Path);
                    Assert.Equal(PluginFileState.Valid, results[2].PluginFile.State);
                    Assert.Null(results[2].Message);

                    Assert.Equal("d", results[3].PluginFile.Path);
                    Assert.Equal(PluginFileState.NotFound, results[3].PluginFile.State);
                    Assert.Equal("A plugin was not found at path 'd'.", results[3].Message);
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_IsIdempotent()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();

                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer(pluginPath, verifierSpy.Object))
                {
                    var firstResult = await discoverer.DiscoverAsync(CancellationToken.None);

                    verifierSpy.Verify(spy => spy.IsValid(It.IsAny<string>()),
                        Times.Once);

                    var secondResult = await discoverer.DiscoverAsync(CancellationToken.None);

                    verifierSpy.Verify(spy => spy.IsValid(It.IsAny<string>()),
                        Times.Once);

                    Assert.Same(firstResult, secondResult);
                }
            }
        }

        private sealed class EmbeddedSignatureVerifierStub : EmbeddedSignatureVerifier
        {
            private readonly Dictionary<string, bool> _responses;

            public EmbeddedSignatureVerifierStub(Dictionary<string, bool> responses)
            {
                _responses = responses;
            }

            public override bool IsValid(string filePath)
            {
                bool value;

                Assert.True(_responses.TryGetValue(filePath, out value));

                return value;
            }
        }
    }
}