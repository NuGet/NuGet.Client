// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class NoOpDisposePluginTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPlugin()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new NoOpDisposePlugin(plugin: null));

            Assert.Equal("plugin", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var connection = Mock.Of<IConnection>();
            var plugin = new Mock<IPlugin>(MockBehavior.Strict);

            plugin.Setup(x => x.Connection)
                .Returns(connection);
            plugin.Setup(x => x.FilePath)
                .Returns("a");
            plugin.Setup(x => x.Name)
                .Returns("b");

            using (var noOpDisposePlugin = new NoOpDisposePlugin(plugin.Object))
            {
                Assert.Same(connection, noOpDisposePlugin.Connection);
                Assert.Equal("a", noOpDisposePlugin.FilePath);
                Assert.Equal("b", noOpDisposePlugin.Name);
            }

            plugin.Verify(x => x.Connection, Times.Once);
            plugin.Verify(x => x.FilePath, Times.Once);
            plugin.Verify(x => x.Name, Times.Once);
        }

        [Fact]
        public void Dispose_DoesNotCallPluginDispose()
        {
            var plugin = new Mock<IPlugin>(MockBehavior.Strict);

            using (var noOpDisposePlugin = new NoOpDisposePlugin(plugin.Object))
            {
            }

            plugin.Verify();
        }
    }
}