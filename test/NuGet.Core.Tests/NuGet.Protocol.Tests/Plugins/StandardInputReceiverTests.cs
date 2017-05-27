// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class StandardInputReceiverTests
    {
        [Fact]
        public void Constructor_ThrowsForNullReader()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new StandardInputReceiver(
                    reader: null));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public void Constructor_AllowsTextReaderNull()
        {
            using (new StandardInputReceiver(TextReader.Null))
            {
            }
        }

        [Fact]
        public void Dispose_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            {
                Assert.True(stream.CanSeek);
                Assert.True(stream.CanRead);
                Assert.True(stream.CanWrite);

                var receiver = new StandardInputReceiver(reader);

                receiver.Dispose();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            {
                receiver.Dispose();
                receiver.Dispose();
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfDisposed()
        {
            var receiver = new StandardInputReceiver(TextReader.Null);

            receiver.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => receiver.ConnectAsync(CancellationToken.None));

            Assert.Equal(nameof(StandardInputReceiver), exception.ObjectName);
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfAlreadyConnected()
        {
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => receiver.ConnectAsync(CancellationToken.None));
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => receiver.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForSingleMessageWithNonBlockingStream()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}";
            var requestId = "a";
            var type = MessageType.Response;
            var method = MessageMethod.None;

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(json))
            using (var receiver = new StandardInputReceiver(reader))
            {
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    receivedEvent.Set();
                };

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Null(message.Payload);
            }
        }

        [Theory]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\"}\r\n", "a", MessageType.Response, MessageMethod.None, null)]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":null}\r\n", "a", MessageType.Response, MessageMethod.None, null)]
        [InlineData("{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":{\"d\":\"e\"}}\r\n", "a", MessageType.Response, MessageMethod.None, "{\"d\":\"e\"}")]
        public async Task MessageReceived_RaisedForSingleMessageWithBlockingStream(string json, string requestId, MessageType type, MessageMethod method, string payload)
        {
            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new StandardInputReceiver(reader))
            {
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    receivedEvent.Set();
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                outboundStream.Write(bytes, offset: 0, count: bytes.Length);

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Equal(payload, message.Payload?.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForSingleMessageInChunksWithBlockingStream()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Progress\",\"Method\":\"None\",\"Payload\":{\"d\":\"e\"}}\r\n";
            var requestId = "a";
            var type = MessageType.Progress;
            var method = MessageMethod.None;
            var payload = "{\"d\":\"e\"}";

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new StandardInputReceiver(reader))
            {
                Message message = null;

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    message = e.Message;

                    receivedEvent.Set();
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                for (var offset = 0; offset < bytes.Length; offset += 10)
                {
                    var count = Math.Min(bytes.Length - offset, 10);

                    outboundStream.Write(bytes, offset, count);
                }

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();

                Assert.Equal(requestId, message.RequestId);
                Assert.Equal(type, message.Type);
                Assert.Equal(method, message.Method);
                Assert.Equal(payload, message.Payload.ToString(Formatting.None));
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForMultipleMessagesWithNonBlockingStream()
        {
            var json = "{\"RequestId\":\"de08f561-50c1-4816-adc3-73d2c283d8cf\",\"Type\":\"Request\",\"Method\":\"Handshake\",\"Payload\":{\"ProtocolVersion\":\"3.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}}\r\n{\"RequestId\":\"e2db1e2d-0282-45c4-9004-b096e221230d\",\"Type\":\"Response\",\"Method\":\"Handshake\",\"Payload\":{\"ResponseCode\":0,\"ProtocolVersion\":\"2.0.0\"}}\r\n";

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(json))
            using (var receiver = new StandardInputReceiver(reader))
            {
                var messages = new List<Message>();

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    messages.Add(e.Message);

                    if (messages.Count == 2)
                    {
                        receivedEvent.Set();
                    }
                };

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForMultipleMessagesWithBlockingStream()
        {
            var json = "{\"RequestId\":\"de08f561-50c1-4816-adc3-73d2c283d8cf\",\"Type\":\"Request\",\"Method\":\"Handshake\",\"Payload\":{\"ProtocolVersion\":\"3.0.0\",\"MinimumProtocolVersion\":\"1.0.0\"}}\r\n{\"RequestId\":\"e2db1e2d-0282-45c4-9004-b096e221230d\",\"Type\":\"Response\",\"Method\":\"Handshake\",\"Payload\":{\"ResponseCode\":0,\"ProtocolVersion\":\"2.0.0\"}}\r\n";

            using (var receivedEvent = new ManualResetEventSlim(initialState: false))
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var stream = new MemoryStream())
            using (var readWriteSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            using (var dataWrittenEvent = new ManualResetEventSlim(initialState: false))
            using (var outboundStream = new SimulatedWriteOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var inboundStream = new SimulatedReadOnlyFileStream(stream, readWriteSemaphore, dataWrittenEvent, cancellationTokenSource.Token))
            using (var reader = new SimulatedStreamReader(inboundStream))
            using (var receiver = new StandardInputReceiver(reader))
            {
                var messages = new List<Message>();

                receiver.MessageReceived += (object sender, MessageEventArgs e) =>
                {
                    messages.Add(e.Message);

                    if (messages.Count == 2)
                    {
                        receivedEvent.Set();
                    }
                };

                var bytes = Encoding.UTF8.GetBytes(json);

                outboundStream.Write(bytes, offset: 0, count: bytes.Length);

                await receiver.ConnectAsync(CancellationToken.None);

                receivedEvent.Wait();
            }
        }

        [Fact]
        public async Task Faulted_RaisedForParseError()
        {
            var invalidJson = "text\r\n";

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(invalidJson))
            using (var receiver = new StandardInputReceiver(reader))
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

        [Theory]
        [InlineData("1")]
        [InlineData("[]")]
        public async Task Faulted_RaisedForDeserializationOfInvalidJson(string invalidJson)
        {
            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(invalidJson))
            using (var receiver = new StandardInputReceiver(reader))
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
        public async Task Faulted_RaisedForDeserializationError()
        {
            var json = "{\"RequestId\":\"a\",\"Type\":\"Response\",\"Method\":\"None\",\"Payload\":\"{\\\"d\\\":\\\"e\\\"}\"}\r\n";

            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(json))
            using (var receiver = new StandardInputReceiver(reader))
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
            using (var faultedEvent = new ManualResetEventSlim(initialState: false))
            using (var reader = new StringReader(json))
            using (var receiver = new StandardInputReceiver(reader))
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
            var receiver = new StandardInputReceiver(TextReader.Null);

            receiver.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

            Assert.Equal(nameof(StandardInputReceiver), exception.ObjectName);
        }

        [Fact]
        public async Task CloseAsync_IsNotIdempotent()
        {
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await receiver.CloseAsync();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.CloseAsync());

                Assert.Equal(nameof(StandardInputReceiver), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_CanBeCalledWithoutConnectAsync()
        {
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            {
                await receiver.CloseAsync();
            }
        }

        [Fact]
        public async Task CloseAsync_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var receiver = new StandardInputReceiver(reader))
            {
                await receiver.ConnectAsync(CancellationToken.None);

                await receiver.CloseAsync();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }
    }
}