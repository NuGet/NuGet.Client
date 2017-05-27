﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class StandardOutputReceiverTests
    {
        [Fact]
        public void Constructor_ThrowsForNullProcess()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new StandardOutputReceiver(
                    process: null));

            Assert.Equal("process", exception.ParamName);
        }

        [Fact]
        public void Dispose_CancelsReading()
        {
            var process = new Mock<IPluginProcess>(MockBehavior.Strict);

            process.Setup(x => x.CancelRead());

            using (var receiver = new StandardOutputReceiver(process.Object))
            {
            }

            process.Verify(x => x.CancelRead(), Times.Once);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>()))
            {
                receiver.Dispose();
                receiver.Dispose();
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfDisposed()
        {
            var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>());

            receiver.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => receiver.ConnectAsync(CancellationToken.None));

            Assert.Equal(nameof(StandardOutputReceiver), exception.ObjectName);
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfAlreadyConnected()
        {
            using (var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>()))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => receiver.ConnectAsync(CancellationToken.None));
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>()))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => receiver.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForSingleMessage()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}";
            var requestId = "a";
            var type = MessageType.Response;
            var method = MessageMethod.None;
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(json)));

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                MessageEventArgs args = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    args = e;

                    receivedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();

                Assert.NotNull(args);
                Assert.NotNull(args.Message);
                Assert.Equal(requestId, args.Message.RequestId);
                Assert.Equal(type, args.Message.Type);
                Assert.Equal(method, args.Message.Method);
                Assert.Null(args.Message.Payload);
            }
        }

        [Fact]
        public async Task MessageReceived_RemovesUtf8Bom()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}";
            var bytes = Encoding.UTF8.GetBytes(json);

            bytes = new byte[] {0xEF, 0xBB, 0xBF }.Concat(bytes).ToArray();
            var jsonWithUtf8Bom = Encoding.UTF8.GetString(bytes);

            var requestId = "a";
            var type = MessageType.Response;
            var method = MessageMethod.None;
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(jsonWithUtf8Bom)));

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                MessageEventArgs args = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    args = e;

                    receivedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();

                Assert.NotNull(args);
                Assert.NotNull(args.Message);
                Assert.Equal(requestId, args.Message.RequestId);
                Assert.Equal(type, args.Message.Type);
                Assert.Equal(method, args.Message.Method);
                Assert.Null(args.Message.Payload);
            }
        }

        [Fact]
        public async Task Faulted_RaisedForParseError()
        {
            var invalidJson = "text";
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(invalidJson)));

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                ProtocolErrorEventArgs args = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    args = e;

                    faultedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                faultedEvent.Wait();

                Assert.NotNull(args);
                Assert.NotNull(args.Exception);
                Assert.IsType<ProtocolException>(args.Exception);
                Assert.Null(args.Message);
            }
        }

        [Theory]
        [InlineData("1")]
        [InlineData("[]")]
        public async Task Faulted_RaisedForDeserializationOfInvalidJson(string invalidJson)
        {
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(invalidJson)));

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                ProtocolErrorEventArgs args = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    args = e;

                    faultedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                faultedEvent.Wait();

                Assert.NotNull(args);
                Assert.NotNull(args.Exception);
                Assert.IsType<ProtocolException>(args.Exception);
                Assert.Null(args.Message);
            }
        }

        [Fact]
        public async Task Faulted_RaisedForDeserializationError()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":\"{\\\"d\\\":\\\"e\\\"}\"}\r\n";

            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(json)));

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                ProtocolErrorEventArgs args = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    args = e;

                    faultedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                faultedEvent.Wait();

                Assert.NotNull(args);
                Assert.NotNull(args.Exception);
                Assert.IsType<ProtocolException>(args.Exception);
                Assert.Null(args.Message);
            }
        }

        [Theory]
        [InlineData("{\"Type\":\"Response\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":null,\"Type\":\"Response\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"\",\"Type\":\"Response\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":null,\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\" \",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"abc\",\"Method\":\"None\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":null}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"\"}\r\n")]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"abc\"}\r\n")]
        public async Task Faulted_RaisedForInvalidMessage(string json)
        {
            var process = new Mock<IPluginProcess>();

            process.Setup(x => x.BeginReadLine())
                .Callback(() => process.Raise(x => x.LineRead += null, new LineReadEventArgs(json)));

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                ProtocolErrorEventArgs args = null;

                receiver.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                {
                    args = e;

                    faultedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                faultedEvent.Wait();

                Assert.NotNull(args);
                Assert.IsType<ProtocolException>(args.Exception);
            }
        }

        [Fact]
        public async Task CloseAsync_ThrowsIfDisposed()
        {
            var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>());

            receiver.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

            Assert.Equal(nameof(StandardOutputReceiver), exception.ObjectName);
        }

        [Fact]
        public async Task CloseAsync_IsNotIdempotent()
        {
            using (var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>()))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await receiver.CloseAsync();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

                Assert.Equal(nameof(StandardOutputReceiver), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_CanBeCalledWithoutConnectAsync()
        {
            using (var receiver = new StandardOutputReceiver(Mock.Of<IPluginProcess>()))
            {
                await receiver.CloseAsync();
            }
        }

        [Fact]
        public async Task CloseAsync_CancelsReading()
        {
            var process = new Mock<IPluginProcess>(MockBehavior.Strict);

            process.Setup(x => x.BeginReadLine());
            process.Setup(x => x.CancelRead());

            using (var receiver = new StandardOutputReceiver(process.Object))
            {
                await receiver.ConnectAsync(CancellationToken.None);
                await receiver.CloseAsync();
            }

            process.Verify(x => x.CancelRead(), Times.Once);
        }
    }
}