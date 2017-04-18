// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a bidirectional channel between a NuGet client and a plugin.
    /// </summary>
    public sealed class Connection : IConnection
    {
        private readonly TaskCompletionSource<object> _closeEvent;
        private bool _isDisposed;
        private readonly IReceiver _receiver;
        private readonly ISender _sender;

        private int _state = (int)ConnectionState.ReadyToConnect;

        /// <summary>
        /// The connection state.
        /// </summary>
        public ConnectionState State => (ConnectionState)_state;

        /// <summary>
        /// Occurs when an unrecoverable fault has been caught.
        /// </summary>
        public event EventHandler<ProtocolErrorEventArgs> Faulted;

        /// <summary>
        /// Occurs when a message has been received.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// Gets the message dispatcher.
        /// </summary>
        public IMessageDispatcher MessageDispatcher { get; }

        /// <summary>
        /// Gets the connection options
        /// </summary>
        public ConnectionOptions Options { get; }

        /// <summary>
        /// Gets the negotiated protocol version, or <c>null</c> if not yet connected.
        /// </summary>
        public SemanticVersion ProtocolVersion { get; private set; }

        /// <summary>
        /// Instantiates a new instance of the <see cref="Connection" /> class.
        /// </summary>
        /// <param name="dispatcher">A message dispatcher.</param>
        /// <param name="sender">A sender.</param>
        /// <param name="receiver">A receiver.</param>
        /// <param name="options">Connection options.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dispatcher" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sender" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="receiver" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options" /> is <c>null</c>.</exception>
        public Connection(IMessageDispatcher dispatcher, ISender sender, IReceiver receiver, ConnectionOptions options)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            if (sender == null)
            {
                throw new ArgumentNullException(nameof(sender));
            }

            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            MessageDispatcher = dispatcher;
            _sender = sender;
            _receiver = receiver;
            Options = options;
            _closeEvent = new TaskCompletionSource<object>();

            MessageDispatcher.SetConnection(this);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            CloseAsync().GetAwaiter().GetResult();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously connects and handshakes with a remote target.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" /> is cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the method has been called already.</exception>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (State != ConnectionState.ReadyToConnect)
            {
                throw new InvalidOperationException(Strings.Plugin_ConnectionAlreadyStarted);
            }

            _state = (int)ConnectionState.Connecting;

            _receiver.MessageReceived += OnMessageReceived;
            _receiver.Faulted += OnFaulted;

            using (var symmetricHandshake = new SymmetricHandshake(
                this,
                Options.HandshakeTimeout,
                Options.ProtocolVersion,
                Options.MinimumProtocolVersion))
            {
                await Task.WhenAll(_receiver.ConnectAsync(cancellationToken), _sender.ConnectAsync(cancellationToken));

                _state = (int)ConnectionState.Handshaking;

                ProtocolVersion = await symmetricHandshake.HandshakeAsync(cancellationToken);

                if (ProtocolVersion == null)
                {
                    throw new ProtocolException(Strings.Plugin_HandshakeFailed);
                }

                _state = (int)ConnectionState.Connected;
            }
        }

        /// <summary>
        /// Asynchronously closes the connection.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task CloseAsync()
        {
            _receiver.MessageReceived -= OnMessageReceived;
            _receiver.Faulted -= OnFaulted;

            var currentState = _state;
            Interlocked.MemoryBarrier();

            if (currentState <= (int)ConnectionState.Closed)
            {
                return _closeEvent.Task;
            }

            var previous = Interlocked.CompareExchange(ref _state, (int)ConnectionState.Closing, currentState);
            if (previous == currentState)
            {
                Task.WhenAll(_sender.CloseAsync(), _receiver.CloseAsync())
                    .ContinueWith(
                        task =>
                        {
                            _sender.Dispose();
                            _receiver.Dispose();

                            MessageDispatcher.Dispose();

                            _state = (int)ConnectionState.Closed;

                            if (task.IsCanceled)
                            {
                                _closeEvent.TrySetCanceled();
                            }
                            else if (task.IsFaulted)
                            {
                                _closeEvent.TrySetException(task.Exception);
                            }
                            else
                            {
                                _closeEvent.TrySetResult(null);
                            }
                        });
            }

            return _closeEvent.Task;
        }

        /// <summary>
        /// Asynchronously sends a message to the remote target.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" /> is cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
        public async Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_state < (int)ConnectionState.Connecting)
            {
                throw new InvalidOperationException(Strings.Plugin_NotConnected);
            }

            await _sender.SendAsync(message, cancellationToken);
        }

        /// <summary>
        /// Asynchronously sends a message to the remote target and receives the target's response.
        /// </summary>
        /// <typeparam name="TOutbound">The outbound payload type.</typeparam>
        /// <typeparam name="TInbound">The inbound payload type.</typeparam>
        /// <param name="method">The outbound message method.</param>
        /// <param name="payload">The outbound message payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <typeparamref name="TInbound" />
        /// from the target.</returns>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" /> is cancelled.</exception>
        public Task<TInbound> SendRequestAndReceiveResponseAsync<TOutbound, TInbound>(
            MessageMethod method,
            TOutbound payload,
            CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class
        {
            cancellationToken.ThrowIfCancellationRequested();

            return MessageDispatcher.DispatchRequestAsync<TOutbound, TInbound>(method, payload, cancellationToken);
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void OnFaulted(object sender, ProtocolErrorEventArgs e)
        {
            Faulted?.Invoke(this, e);
        }
    }
}