// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class SenderTests
    {
        private readonly Message _message = new Message(requestId: "a", type: MessageType.Request, method: MessageMethod.None);

        [Fact]
        public void Constructor_ThrowsForNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Sender(writer: null));

            Assert.Equal("writer", exception.ParamName);
        }

        [Fact]
        public void Constructor_AllowsTextWriterNull()
        {
            new Sender(TextWriter.Null);
        }

        [Fact]
        public void Dispose_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                Assert.True(stream.CanSeek);
                Assert.True(stream.CanRead);
                Assert.True(stream.CanWrite);

                var sender = new Sender(writer);

                sender.Dispose();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                sender.Dispose();
                sender.Dispose();
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfDisposed()
        {
            var sender = new Sender(TextWriter.Null);

            sender.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => sender.ConnectAsync(CancellationToken.None));

            Assert.Equal(nameof(Sender), exception.ObjectName);
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfAlreadyConnected()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await sender.ConnectAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => sender.ConnectAsync(CancellationToken.None));
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => sender.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsForNull()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await sender.ConnectAsync(CancellationToken.None);

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => sender.SendAsync(message: null, cancellationToken: CancellationToken.None));

                Assert.Equal("message", exception.ParamName);
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsIfDisposed()
        {
            var sender = new Sender(TextWriter.Null);

            sender.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => sender.SendAsync(_message, CancellationToken.None));

            Assert.Equal(nameof(Sender), exception.ObjectName);
        }

        [Fact]
        public async Task SendAsync_ThrowsIfCancelled()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await sender.ConnectAsync(CancellationToken.None);

                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => sender.SendAsync(_message, new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsForDisposedWriter()
        {
            using (var writer = new StringWriter())
            {
                writer.Dispose();

                using (var sender = new Sender(writer))
                {
                    await sender.ConnectAsync(CancellationToken.None);

                    await Assert.ThrowsAsync<ObjectDisposedException>(
                        () => sender.SendAsync(_message, CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task SendAsync_WritesMessageToWriter()
        {
            using (var writer = new StringWriter())
            using (var sender = new Sender(writer))
            {
                await sender.ConnectAsync(CancellationToken.None);

                await sender.SendAsync(_message, CancellationToken.None);

                var actualResult = writer.ToString();

                Assert.Equal($"{{\"RequestId\":\"a\",\"Type\":\"Request\",\"Method\":\"None\"}}{Environment.NewLine}", actualResult);
            }
        }

        [Fact]
        public async Task CloseAsync_ThrowsIfDisposed()
        {
            using (var writer = new StringWriter())
            {
                var sender = new Sender(TextWriter.Null);

                sender.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => sender.CloseAsync());

                Assert.Equal(nameof(Sender), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_IsNotIdempotent()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await sender.ConnectAsync(CancellationToken.None);

                await sender.CloseAsync();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => sender.CloseAsync());

                Assert.Equal(nameof(Sender), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CloseAsync_CanBeCalledWithoutConnectAsync()
        {
            using (var sender = new Sender(TextWriter.Null))
            {
                await sender.CloseAsync();
            }
        }

        [Fact]
        public async Task CloseAsync_ClosesUnderlyingStream()
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var sender = new Sender(writer))
            {
                await sender.ConnectAsync(CancellationToken.None);
                await sender.SendAsync(_message, CancellationToken.None);

                await sender.CloseAsync();

                Assert.False(stream.CanSeek);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
            }
        }
    }
}