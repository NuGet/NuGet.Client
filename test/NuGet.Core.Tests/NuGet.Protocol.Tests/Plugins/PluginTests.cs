// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Moq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyFilePath(string filePath)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new Plugin(
                    filePath,
                    Mock.Of<IConnection>(),
                    Mock.Of<IPluginProcess>(),
                    isOwnProcess: false,
                    idleTimeout: Timeout.InfiniteTimeSpan));

            Assert.Equal("filePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullConnection()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Plugin(
                    filePath: "a",
                    connection: null,
                    process: Mock.Of<IPluginProcess>(),
                    isOwnProcess: false,
                    idleTimeout: Timeout.InfiniteTimeSpan));

            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullProcess()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Plugin(
                    filePath: "a",
                    connection: Mock.Of<IConnection>(),
                    process: null,
                    isOwnProcess: false,
                    idleTimeout: Timeout.InfiniteTimeSpan));

            Assert.Equal("process", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForTooSmallIdleTimeout()
        {
            var idleTimeout = TimeSpan.FromMilliseconds(Timeout.InfiniteTimeSpan.TotalMilliseconds - 1);
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Plugin(
                    filePath: "a",
                    connection: Mock.Of<IConnection>(),
                    process: Mock.Of<IPluginProcess>(),
                    isOwnProcess: false,
                    idleTimeout: idleTimeout));

            Assert.Equal("idleTimeout", exception.ParamName);
            Assert.Equal(idleTimeout, exception.ActualValue);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var filePath = @"C:\a\b\c.d";
            var connection = Mock.Of<IConnection>();
            var process = Mock.Of<IPluginProcess>();
            var isOwnProcess = false;
            var idleTimeout = Timeout.InfiniteTimeSpan;

            using (var plugin = new Plugin(filePath, connection, process, isOwnProcess, idleTimeout))
            {
                Assert.Same(connection, plugin.Connection);
                Assert.Equal(filePath, plugin.FilePath);
                Assert.Equal("c", plugin.Name);
            }
        }

        [Fact]
        public void Dispose_DisposesResources()
        {
            var connection = new Mock<IConnection>(MockBehavior.Strict);
            var process = new Mock<IPluginProcess>(MockBehavior.Strict);

            connection.Setup(x => x.Dispose());
            process.Setup(x => x.Kill());
            process.Setup(x => x.Dispose());

            using (var plugin = new Plugin(
                filePath: @"C:\a\b\c.d",
                connection: connection.Object,
                process: process.Object,
                isOwnProcess: false,
                idleTimeout: Timeout.InfiniteTimeSpan))
            {
            }

            connection.Verify(x => x.Dispose(), Times.Once);
            process.Verify(x => x.Kill(), Times.Once);
            process.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var plugin = new Plugin(
                filePath: @"C:\a\b\c.d",
                connection: Mock.Of<IConnection>(),
                process: Mock.Of<IPluginProcess>(),
                isOwnProcess: false,
                idleTimeout: Timeout.InfiniteTimeSpan))
            {
                plugin.Dispose();
                plugin.Dispose();
            }
        }

        [Fact]
        public void Exited_RaisedOnProcessExited()
        {
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.HasExited)
                .Returns(true);

            using (var exitedEvent = new ManualResetEventSlim(initialState: false))
            using (var plugin = new Plugin(
                filePath: @"C:\a\b\c.d",
                connection: Mock.Of<IConnection>(),
                process: process.Object,
                isOwnProcess: false,
                idleTimeout: Timeout.InfiniteTimeSpan))
            {
                PluginEventArgs args = null;

                plugin.Exited += (object sender, PluginEventArgs e) =>
                {
                    args = e;

                    exitedEvent.Set();
                };

                process.Raise(x => x.Exited += null, EventArgs.Empty);

                exitedEvent.Wait();

                Assert.NotNull(args);
                Assert.Same(plugin, args.Plugin);
            }
        }

        [Fact]
        public void Fault_RaisedOnIdleTimeout()
        {
            var connection = new Mock<IConnection>();

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var plugin = new Plugin(
                filePath: @"C:\a\b\c.d",
                connection: connection.Object,
                process: Mock.Of<IPluginProcess>(),
                isOwnProcess: false,
                idleTimeout: Timeout.InfiniteTimeSpan))
            {
                PluginEventArgs args = null;

                plugin.Faulted += (object sender, PluginEventArgs e) =>
                {
                    args = e;

                    faultedEvent.Set();
                };

                var protocolErrorArgs = new ProtocolErrorEventArgs(new ProtocolException("test"));

                connection.Raise(x => x.Faulted += null, protocolErrorArgs);

                faultedEvent.Wait();

                Assert.NotNull(args);
                Assert.Same(plugin, args.Plugin);
            }
        }

        [Fact]
        public void Idle_RaisedOnIdleTimeout()
        {
            using (var idleEvent = new ManualResetEventSlim(initialState: false))
            using (var plugin = new Plugin(
                filePath: @"C:\a\b\c.d",
                connection: Mock.Of<IConnection>(),
                process: Mock.Of<IPluginProcess>(),
                isOwnProcess: false,
                idleTimeout: TimeSpan.FromSeconds(1)))
            {
                var wasFired = false;

                plugin.Idle += (object sender, PluginEventArgs e) =>
                {
                    wasFired = true;

                    idleEvent.Set();
                };

                idleEvent.Wait();

                Assert.True(wasFired);
            }
        }
    }
}