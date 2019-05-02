// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginManagerTests
    {
        [Fact]
        public async Task TryGetSourceAgnosticPluginAsync_WhenExceptionIsThrownDuringPluginCreation_PropagatesException()
        {
            const string pluginFilePath = "a";
            const string message = "b";

            var reader = Mock.Of<IEnvironmentVariableReader>();
            var pluginFactory = new Mock<IPluginFactory>(MockBehavior.Strict);
            var exception = new Exception(message);

            pluginFactory.Setup(x => x.GetOrCreateAsync(
                    It.Is<string>(filePath => string.Equals(filePath, pluginFilePath, StringComparison.Ordinal)),
                    It.Is<IEnumerable<string>>(arguments => arguments != null && arguments.Any()),
                    It.IsNotNull<IRequestHandlers>(),
                    It.IsNotNull<ConnectionOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            pluginFactory.Setup(x => x.Dispose());

            using (var directory = TestDirectory.Create())
            using (var pluginManager = new PluginManager(
                reader,
                new Lazy<IPluginDiscoverer>(() => Mock.Of<IPluginDiscoverer>()),
                (TimeSpan idleTimeout) => pluginFactory.Object,
                new Lazy<string>(() => directory.Path)))
            {
                var discoveryResult = new PluginDiscoveryResult(
                    new PluginFile(
                        pluginFilePath,
                        new Lazy<PluginFileState>(() => PluginFileState.Valid)));

                Tuple<bool, PluginCreationResult> result = await pluginManager.TryGetSourceAgnosticPluginAsync(
                    discoveryResult,
                    OperationClaim.Authentication,
                    CancellationToken.None);
                bool wasSomethingCreated = result.Item1;
                PluginCreationResult creationResult = result.Item2;

                Assert.True(wasSomethingCreated);
                Assert.NotNull(creationResult);

                Assert.Equal($"Problem starting the plugin '{pluginFilePath}'. {message}", creationResult.Message);
                Assert.Same(exception, creationResult.Exception);
            }

            pluginFactory.Verify();
        }
    }
}