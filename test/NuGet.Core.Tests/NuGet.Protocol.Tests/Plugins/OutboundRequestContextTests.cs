// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class OutboundRequestContextTests
    {
        [Fact]
        public void Constructor_ThrowsForNullConnection()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new OutboundRequestContext<HandshakeResponse>(
                    connection: null,
                    request: new Message(
                        requestId: "a",
                        type: MessageType.Request,
                        method: MessageMethod.Handshake,
                        payload: null),
                    timeout: TimeSpan.FromMinutes(1),
                    isKeepAlive: true,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullRequest()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new OutboundRequestContext<HandshakeResponse>(
                    Mock.Of<IConnection>(),
                    request: null,
                    timeout: TimeSpan.FromMinutes(1),
                    isKeepAlive: true,
                    cancellationToken: CancellationToken.None));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            using (var test = new OutboundRequestContextTest())
            {
                Assert.Equal(test.Request.RequestId, test.Context.RequestId);
                Assert.NotNull(test.Context.CompletionTask);
                Assert.Equal(TaskStatus.WaitingForActivation, test.Context.CompletionTask.Status);
            }
        }

        [Fact]
        public async Task Constructor_ClosesIfTimedOut()
        {
            using (var test = new OutboundRequestContextTest(TimeSpan.FromMilliseconds(10)))
            {
                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => test.Context.CompletionTask);
            }
        }

        [Fact]
        public void Constructor_ClosesIfCancelled()
        {
            using (var test = new OutboundRequestContextTest())
            {
                test.CancellationTokenSource.Cancel();

                Assert.Equal(TaskStatus.Canceled, test.Context.CompletionTask.Status);
            }
        }

        [Fact]
        public void Dispose_DoesNotDisposeConnection()
        {
            using (var test = new OutboundRequestContextTest())
            {
                test.Context.Dispose();
                test.Connection.Verify();
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var test = new OutboundRequestContextTest())
            {
                test.Context.Dispose();
                test.Context.Dispose();
            }
        }

        [Fact]
        public void HandleCancel_CancelsCompletionTask()
        {
            using (var test = new OutboundRequestContextTest())
            {
                test.Context.HandleCancel();

                Assert.Equal(TaskStatus.Canceled, test.Context.CompletionTask.Status);
            }
        }

        [Fact]
        public void HandleProgress_ThrowsForNullProgress()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => test.Context.HandleProgress(progress: null));

                Assert.Equal("progress", exception.ParamName);
            }
        }

        [Fact]
        public void HandleResponse_ThrowsForNullProgress()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => test.Context.HandleResponse(response: null));

                Assert.Equal("response", exception.ParamName);
            }
        }

        [Fact]
        public void HandleResponse_CompletesCompletionTask()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var payload = new HandshakeResponse(MessageResponseCode.Error, protocolVersion: null);
                var response = MessageUtilities.Create(
                    test.Request.RequestId,
                    MessageType.Response,
                    test.Request.Method,
                    payload);

                test.Context.HandleResponse(response);

                Assert.Equal(TaskStatus.RanToCompletion, test.Context.CompletionTask.Status);
                Assert.Equal(MessageResponseCode.Error, test.Context.CompletionTask.Result.ResponseCode);
                Assert.Null(test.Context.CompletionTask.Result.ProtocolVersion);
            }
        }

        [Fact]
        public void HandleResponse_SecondResponseIsIgnored()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var payload = new HandshakeResponse(MessageResponseCode.Error, protocolVersion: null);
                var firstResponse = MessageUtilities.Create(
                    test.Request.RequestId,
                    MessageType.Response,
                    test.Request.Method,
                    payload);

                test.Context.HandleResponse(firstResponse);

                var response = test.Context.CompletionTask.Result;

                payload = new HandshakeResponse(MessageResponseCode.Success, ProtocolConstants.CurrentVersion);
                var secondResponse = MessageUtilities.Create(
                    test.Request.RequestId,
                    MessageType.Response,
                    test.Request.Method,
                    payload);

                test.Context.HandleResponse(secondResponse);

                Assert.Same(response, test.Context.CompletionTask.Result);
            }
        }

        [Fact]
        public void HandleFault_ThrowsForNullProgress()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => test.Context.HandleFault(fault: null));

                Assert.Equal("fault", exception.ParamName);
            }
        }

        [Fact]
        public void HandleFault_ThrowsFault()
        {
            using (var test = new OutboundRequestContextTest())
            {
                var payload = new Fault("test");
                var response = MessageUtilities.Create(
                    test.Request.RequestId,
                    MessageType.Fault,
                    test.Request.Method,
                    payload);

                var exception = Assert.Throws<ProtocolException>(
                    () => test.Context.HandleFault(response));

                Assert.Equal("test", exception.Message);
            }
        }

        private sealed class OutboundRequestContextTest : IDisposable
        {
            internal CancellationTokenSource CancellationTokenSource { get; }
            internal Mock<IConnection> Connection { get; }
            internal OutboundRequestContext<HandshakeResponse> Context { get; }
            internal Message Request { get; }

            internal OutboundRequestContextTest(TimeSpan? timeout = null)
            {
                CancellationTokenSource = new CancellationTokenSource();
                Connection = new Mock<IConnection>(MockBehavior.Strict);
                Request = new Message(
                    requestId: "a",
                    type: MessageType.Request,
                    method: MessageMethod.Handshake,
                    payload: null);
                Context = new OutboundRequestContext<HandshakeResponse>(
                    Connection.Object,
                    Request,
                    timeout,
                    isKeepAlive: false,
                    cancellationToken: CancellationTokenSource.Token);
            }

            public void Dispose()
            {
                try
                {
                    using (CancellationTokenSource)
                    {
                        CancellationTokenSource.Cancel();
                    }
                }
                catch (Exception)
                {
                }

                Context.Dispose();

                GC.SuppressFinalize(this);
            }
        }
    }
}