// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ConnectionTests
    {
        [Fact]
        public void Constructor_ThrowsForNullDispatcher()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new Connection(
                        dispatcher: null,
                        sender: new Sender(TextWriter.Null),
                        receiver: new StandardInputReceiver(TextReader.Null),
                        options: ConnectionOptions.CreateDefault()));

                Assert.Equal("dispatcher", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullSender()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new Connection(
                        new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator()),
                        sender: null,
                        receiver: new StandardInputReceiver(TextReader.Null),
                        options: ConnectionOptions.CreateDefault()));

                Assert.Equal("sender", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullReceiver()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new Connection(
                new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator()),
                new Sender(TextWriter.Null),
                receiver: null,
                options: ConnectionOptions.CreateDefault()));

            Assert.Equal("receiver", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullOptions()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new Connection(
                        new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator()),
                        new Sender(TextWriter.Null),
                        new StandardInputReceiver(TextReader.Null),
                        options: null));

                Assert.Equal("options", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var dispatcher = Mock.Of<IMessageDispatcher>();
            var sender = Mock.Of<ISender>();
            var receiver = Mock.Of<IReceiver>();
            var options = ConnectionOptions.CreateDefault();

            using (var connection = new Connection(dispatcher, sender, receiver, options))
            {
                Assert.Same(dispatcher, connection.MessageDispatcher);
                Assert.Same(options, connection.Options);
                Assert.Null(connection.ProtocolVersion);
                Assert.Equal(ConnectionState.ReadyToConnect, connection.State);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var test = new ConnectionTest())
            {
                test.Connection.Dispose();
                test.Connection.Dispose();
            }
        }

        [Fact]
        public async Task ConnectAsync_ThrowsIfCancelled()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var sender = new Sender(TextWriter.Null))
            using (var receiver = new StandardInputReceiver(TextReader.Null))
            using (var dispatcher = new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator()))
            using (var connection = new Connection(dispatcher, sender, receiver, ConnectionOptions.CreateDefault()))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => connection.ConnectAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task ConnectAsync_HandshakeNegotiatesProtocolVersionForIdenticalVersionRanges()
        {
            using (var test = new ConnectAsyncTest(ConnectionOptions.CreateDefault(), ConnectionOptions.CreateDefault()))
            {
                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                Assert.NotNull(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.NotNull(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, test.LocalToRemoteConnection.ProtocolVersion);
            }
        }

        [Fact]
        public async Task ConnectAsync_HandshakeNegotiatesHighestCompatibleProtocolVersion()
        {
            var localToRemoteOptions = new ConnectionOptions(
                new SemanticVersion(2, 0, 0),
                new SemanticVersion(1, 0, 0),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
            var remoteToLocalOptions = new ConnectionOptions(
                new SemanticVersion(3, 0, 0),
                new SemanticVersion(1, 0, 0),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                Assert.NotNull(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.NotNull(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(test.RemoteToLocalConnection.ProtocolVersion, localToRemoteOptions.ProtocolVersion);
            }
        }

        [Fact]
        public async Task ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfVersionRangesDoNotOverlap()
        {
            var localToRemoteOptions = new ConnectionOptions(
                new SemanticVersion(2, 0, 0),
                new SemanticVersion(2, 0, 0),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
            var remoteToLocalOptions = new ConnectionOptions(
                new SemanticVersion(1, 0, 0),
                new SemanticVersion(1, 0, 0),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                var tasks = new[]
                {
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken)
                };

                await Assert.ThrowsAsync<ProtocolException>(() => Task.WhenAll(tasks));

                Assert.NotNull(tasks[0].Exception);
                Assert.NotNull(tasks[1].Exception);

                Assert.Null(test.RemoteToLocalConnection.ProtocolVersion);
                Assert.Equal(ConnectionState.FailedToHandshake, test.RemoteToLocalConnection.State);
                Assert.Null(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(ConnectionState.FailedToHandshake, test.LocalToRemoteConnection.State);
            }
        }

        [Fact]
        public async Task ConnectAsync_HandshakeFailsToNegotiateProtocolVersionIfOnePartyFailsToRespondWithinTimeoutPeriod()
        {
            var protocolVersion = new SemanticVersion(1, 0, 0);
            var localToRemoteOptions = new ConnectionOptions(
                protocolVersion,
                protocolVersion,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10));
            var remoteToLocalOptions = new ConnectionOptions(
                protocolVersion,
                protocolVersion,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));

            using (var test = new ConnectAsyncTest(localToRemoteOptions, remoteToLocalOptions))
            {
                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                Assert.Null(test.LocalToRemoteConnection.ProtocolVersion);
                Assert.Equal(ConnectionState.FailedToHandshake, test.LocalToRemoteConnection.State);
            }
        }

        [Fact]
        public async Task CloseAsync_ClosesConnection()
        {
            using (var test = new ConnectAsyncTest(ConnectionOptions.CreateDefault(), ConnectionOptions.CreateDefault()))
            {
                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                Assert.Equal(ConnectionState.Connected, test.LocalToRemoteConnection.State);

                await test.LocalToRemoteConnection.CloseAsync();

                Assert.Equal(ConnectionState.Closed, test.LocalToRemoteConnection.State);
            }
        }

        [Fact]
        public async Task Faulted_RaisedForProtocolError()
        {
            using (var test = new ConnectAsyncTest(ConnectionOptions.CreateDefault(), ConnectionOptions.CreateDefault()))
            {
                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                using (var faultedEvent = new ManualResetEventSlim(initialState: false))
                {
                    ProtocolErrorEventArgs args = null;

                    test.LocalToRemoteConnection.Faulted += (object sender, ProtocolErrorEventArgs e) =>
                    {
                        args = e;

                        faultedEvent.Set();
                    };

                    test.LocalToRemoteConnection.MessageDispatcher.RequestHandlers.TryAdd(
                        MessageMethod.Initialize,
                        new RequestHandler<InitializeResponse>(new InitializeResponse(MessageResponseCode.Success)));

                    var message = new Message(
                        requestId: "a",
                        type: MessageType.Response,
                        method: MessageMethod.Initialize);

                    await test.RemoteToLocalConnection.SendAsync(message, test.CancellationToken);

                    faultedEvent.Wait();

                    Assert.NotNull(args);
                    Assert.IsType<ProtocolException>(args.Exception);
                    Assert.NotNull(args.Message);
                    Assert.Equal(message.RequestId, args.Message.RequestId);
                    Assert.Equal(message.Type, args.Message.Type);
                    Assert.Equal(message.Method, args.Message.Method);
                }
            }
        }

        [Fact]
        public async Task MessageReceived_RaisedForInboundMessage()
        {
            using (var test = new ConnectAsyncTest(ConnectionOptions.CreateDefault(), ConnectionOptions.CreateDefault()))
            {
                await Task.WhenAll(
                    test.RemoteToLocalConnection.ConnectAsync(test.CancellationToken),
                    test.LocalToRemoteConnection.ConnectAsync(test.CancellationToken));

                using (var messageReceivedEvent = new ManualResetEventSlim(initialState: false))
                {
                    MessageEventArgs args = null;

                    test.LocalToRemoteConnection.MessageReceived += (object sender, MessageEventArgs e) =>
                    {
                        args = e;

                        messageReceivedEvent.Set();
                    };

                    test.LocalToRemoteConnection.MessageDispatcher.RequestHandlers.TryAdd(
                        MessageMethod.Initialize,
                        new RequestHandler<InitializeResponse>(new InitializeResponse(MessageResponseCode.Success)));

                    var message = new Message(
                        requestId: "a",
                        type: MessageType.Request,
                        method: MessageMethod.Initialize);

                    await test.RemoteToLocalConnection.SendAsync(message, test.CancellationToken);

                    messageReceivedEvent.Wait();

                    Assert.NotNull(args);
                    Assert.NotNull(args.Message);
                    Assert.Equal(message.RequestId, args.Message.RequestId);
                    Assert.Equal(message.Type, args.Message.Type);
                    Assert.Equal(message.Method, args.Message.Method);
                }
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsForNullMessage()
        {
            using (var test = new ConnectionTest())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Connection.SendAsync(message: null, cancellationToken: CancellationToken.None));

                Assert.Equal("message", exception.ParamName);
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsIfCancelled()
        {
            var message = new Message(
                requestId: "a",
                type: MessageType.Request,
                method: MessageMethod.Initialize);

            using (var test = new ConnectionTest())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Connection.SendAsync(message, new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task SendAsync_ThrowsIfNotConnected()
        {
            var message = new Message(
                requestId: "a",
                type: MessageType.Request,
                method: MessageMethod.Initialize);

            using (var test = new ConnectionTest())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => test.Connection.SendAsync(message, CancellationToken.None));
            }
        }

        [Fact]
        public async Task SendRequestAndReceiveResponseAsync_ThrowsIfCancelled()
        {
            using (var test = new ConnectionTest())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Connection.SendRequestAndReceiveResponseAsync<LogRequest, LogResponse>(
                        MessageMethod.Log,
                        new LogRequest(LogLevel.Debug, "a"),
                        new CancellationToken(canceled: true)));
            }
        }

        private sealed class ConnectAsyncTest : IDisposable
        {
            private readonly CancellationTokenSource _remoteCancellationTokenSource;
            private readonly CancellationTokenSource _localCancellationTokenSource;
            private readonly CancellationTokenSource _combinedCancellationTokenSource;
            private readonly SimulatedIpc _simulatedIpc;
            private readonly Sender _remoteSender;
            private readonly Receiver _remoteReceiver;
            private readonly MessageDispatcher _remoteDispatcher;
            private readonly Sender _localSender;
            private readonly Receiver _localReceiver;
            private readonly MessageDispatcher _localDispatcher;
            private bool _isDisposed;

            internal Connection RemoteToLocalConnection { get; }
            internal Connection LocalToRemoteConnection { get; }
            internal CancellationToken CancellationToken { get; }

            internal ConnectAsyncTest(ConnectionOptions localToRemoteOptions, ConnectionOptions remoteToLocalOptions)
            {
                _remoteCancellationTokenSource = new CancellationTokenSource();
                _localCancellationTokenSource = new CancellationTokenSource();
                _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _remoteCancellationTokenSource.Token, _localCancellationTokenSource.Token);
                _simulatedIpc = SimulatedIpc.Create(_combinedCancellationTokenSource.Token);
                _remoteSender = new Sender(_simulatedIpc.RemoteStandardOutputForRemote);
                _remoteReceiver = new StandardInputReceiver(_simulatedIpc.RemoteStandardInputForRemote);
                _remoteDispatcher = new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator());
                LocalToRemoteConnection = new Connection(_remoteDispatcher, _remoteSender, _remoteReceiver, localToRemoteOptions);
                _localSender = new Sender(_simulatedIpc.RemoteStandardInputForLocal);
                _localReceiver = new StandardInputReceiver(_simulatedIpc.RemoteStandardOutputForLocal);
                _localDispatcher = new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator());
                RemoteToLocalConnection = new Connection(_localDispatcher, _localSender, _localReceiver, remoteToLocalOptions);
                CancellationToken = _combinedCancellationTokenSource.Token;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _combinedCancellationTokenSource.Cancel();

                _simulatedIpc.Dispose();
                LocalToRemoteConnection.Dispose();
                RemoteToLocalConnection.Dispose();
                _remoteSender.Dispose();
                _remoteReceiver.Dispose();
                _remoteDispatcher.Dispose();
                _localSender.Dispose();
                _localReceiver.Dispose();
                _localDispatcher.Dispose();
                _combinedCancellationTokenSource.Dispose();
                _remoteCancellationTokenSource.Dispose();
                _localCancellationTokenSource.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        private sealed class ConnectionTest : IDisposable
        {
            private readonly SimulatedIpc _simulatedIpc;
            private readonly Sender _sender;
            private readonly Receiver _receiver;
            private readonly MessageDispatcher _dispatcher;
            private bool _isDisposed;

            internal Connection Connection { get; }

            internal ConnectionTest()
            {
                var cancellationTokenSource = new CancellationTokenSource();
                _simulatedIpc = SimulatedIpc.Create(cancellationTokenSource.Token);
                _sender = new Sender(_simulatedIpc.RemoteStandardOutputForRemote);
                _receiver = new StandardInputReceiver(_simulatedIpc.RemoteStandardInputForRemote);
                _dispatcher = new MessageDispatcher(new RequestHandlers(), new RequestIdGenerator());
                var options = ConnectionOptions.CreateDefault();
                Connection = new Connection(_dispatcher, _sender, _receiver, options);
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _simulatedIpc.Dispose();
                Connection.Dispose();
                _sender.Dispose();
                _receiver.Dispose();
                _dispatcher.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        private sealed class RequestHandler<TPayload> : IRequestHandler
            where TPayload : class
        {
            private readonly TPayload _payload;

            public CancellationToken CancellationToken => CancellationToken.None;

            internal RequestHandler(TPayload payload)
            {
                _payload = payload;
            }

            public Task HandleCancelAsync(Message request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task HandleProgressAsync(Message request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task HandleResponseAsync(Message request, IResponseHandler responseHandler, CancellationToken cancellationToken)
            {
                return responseHandler.SendResponseAsync(request, _payload, cancellationToken);
            }
        }
    }
}