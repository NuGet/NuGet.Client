// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A message dispatcher that maintains state for outstanding requests
    /// and routes messages to configured request handlers.
    /// </summary>
    public sealed class MessageDispatcher : IMessageDispatcher, IResponseHandler
    {
        private IConnection _connection;
        private readonly IIdGenerator _idGenerator;
        private bool _isDisposed;
        private readonly ConcurrentDictionary<string, RequestContext> _requestContexts;

        /// <summary>
        /// Gets the request handlers for use by the dispatcher.
        /// </summary>
        public IRequestHandlers RequestHandlers { get; }

        /// <summary>
        /// Instantiates a new <see cref="MessageDispatcher" /> class.
        /// </summary>
        /// <param name="requestHandlers">Request handlers.</param>
        /// <param name="idGenerator">A unique identifier generator.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="requestHandlers" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="idGenerator" />
        /// is <c>null</c>.</exception>
        public MessageDispatcher(IRequestHandlers requestHandlers, IIdGenerator idGenerator)
        {
            if (requestHandlers == null)
            {
                throw new ArgumentNullException(nameof(requestHandlers));
            }

            if (idGenerator == null)
            {
                throw new ArgumentNullException(nameof(idGenerator));
            }

            RequestHandlers = requestHandlers;
            _idGenerator = idGenerator;

            _requestContexts = new ConcurrentDictionary<string, RequestContext>();
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

            if (_connection != null)
            {
                _connection.MessageReceived -= OnMessageReceived;
            }

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously dispatches a cancellation request for the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchCancelAsync(Message request, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchCancelAsync(connection, request, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a fault notification for the specified request.
        /// </summary>
        /// <param name="request">The cancel request.</param>
        /// <param name="fault">The fault payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchFaultAsync(Message request, Fault fault, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchFaultAsync(connection, request, fault, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a progress notification for the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="progress">The progress payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchProgressAsync(Message request, Progress progress, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchProgressAsync(connection, request, progress, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a request.
        /// </summary>
        /// <typeparam name="TOutbound">The request payload type.</typeparam>
        /// <typeparam name="TInbound">The expected response payload type.</typeparam>
        /// <param name="method">The request method.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <typeparamref name="TInbound" />
        /// from the target.</returns>
        public Task<TInbound> DispatchRequestAsync<TOutbound, TInbound>(
            MessageMethod method,
            TOutbound payload,
            CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult<TInbound>(null);
            }

            return DispatchWithNewContextAsync<TOutbound, TInbound>(
                connection,
                MessageType.Request,
                method,
                payload,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a response.
        /// </summary>
        /// <typeparam name="TOutbound">The request payload type.</typeparam>
        /// <param name="request">The associated request.</param>
        /// <param name="responsePayload">The response payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchResponseAsync<TOutbound>(
            Message request,
            TOutbound responsePayload,
            CancellationToken cancellationToken)
            where TOutbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchAsync(connection, MessageType.Response, request, responsePayload, cancellationToken);
        }

        /// <summary>
        /// Sets the connection to be used for dispatching messages.
        /// </summary>
        /// <param name="connection">A connection instance.  Can be <c>null</c>.</param>
        public void SetConnection(IConnection connection)
        {
            if (_connection == connection)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.MessageReceived -= OnMessageReceived;
            }

            _connection = connection;

            if (_connection != null)
            {
                _connection.MessageReceived += OnMessageReceived;
            }
        }

        Task IResponseHandler.SendResponseAsync<TPayload>(
            Message request,
            TPayload payload,
            CancellationToken cancellationToken)
        {
            return DispatchResponseAsync(request, payload, cancellationToken);
        }

        private Message CreateMessage(MessageType type, MessageMethod method)
        {
            var requestId = _idGenerator.GenerateUniqueId();

            return new Message(requestId, type, method);
        }

        private Message CreateMessage<TPayload>(MessageType type, MessageMethod method, TPayload payload)
            where TPayload : class
        {
            var requestId = _idGenerator.GenerateUniqueId();

            return MessageUtilities.Create(requestId, type, method, payload);
        }

        private async Task DispatchAsync<TOutgoing>(
            IConnection connection,
            MessageType type,
            Message request,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(request.RequestId, out requestContext))
            {
                return;
            }

            var message = MessageUtilities.Create(request.RequestId, type, request.Method, payload);

            try
            {
                await connection.SendAsync(message, cancellationToken);
            }
            finally
            {
                RemoveRequestContext(request.RequestId);
            }
        }

        private async Task DispatchCancelAsync(
            IConnection connection,
            Message request,
            CancellationToken cancellationToken)
        {
            var message = new Message(request.RequestId, MessageType.Cancel, request.Method);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchFaultAsync(
            IConnection connection,
            Message request,
            Fault fault,
            CancellationToken cancellationToken)
        {
            Message message;

            var jsonPayload = JsonSerializationUtilities.FromObject(fault);

            if (request == null)
            {
                var requestId = _idGenerator.GenerateUniqueId();

                message = new Message(requestId, MessageType.Fault, MessageMethod.None, jsonPayload);

                await connection.SendAsync(message, cancellationToken);
            }
            else
            {
                message = new Message(request.RequestId, MessageType.Fault, request.Method, jsonPayload);

                await DispatchWithExistingContextAsync(connection, message, cancellationToken);
            }
        }

        private async Task DispatchProgressAsync(
            IConnection connection,
            Message request,
            Progress progress,
            CancellationToken cancellationToken)
        {
            var message = MessageUtilities.Create(request.RequestId, MessageType.Progress, request.Method, progress);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchWithoutContextAsync<TOutgoing>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
        {
            var message = CreateMessage(type, method, payload);

            await connection.SendAsync(message, cancellationToken);
        }

        private async Task DispatchWithExistingContextAsync(
            IConnection connection,
            Message response,
            CancellationToken cancellationToken)
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(response.RequestId, out requestContext))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestContextDoesNotExist, response.RequestId));
            }

            await connection.SendAsync(response, cancellationToken);
        }

        private async Task<TIncoming> DispatchWithNewContextAsync<TIncoming>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            CancellationToken cancellationToken)
            where TIncoming : class
        {
            var message = CreateMessage(type, method);
            var timeout = GetRequestTimeout(connection, type, method);
            var isKeepAlive = GetIsKeepAlive(connection, type, method);
            var requestContext = CreateRequestContext<TIncoming>(message, timeout, isKeepAlive, cancellationToken);

            _requestContexts.TryAdd(message.RequestId, requestContext);

            switch (type)
            {
                case MessageType.Request:
                case MessageType.Response:
                case MessageType.Fault:
                    try
                    {
                        await connection.SendAsync(message, cancellationToken);

                        return await requestContext.CompletionTask;
                    }
                    finally
                    {
                        RemoveRequestContext(message.RequestId);
                    }

                default:
                    break;
            }

            return null;
        }

        private async Task<TIncoming> DispatchWithNewContextAsync<TOutgoing, TIncoming>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
            where TIncoming : class
        {
            var message = CreateMessage(type, method, payload);
            var timeout = GetRequestTimeout(connection, type, method);
            var isKeepAlive = GetIsKeepAlive(connection, type, method);
            var requestContext = CreateRequestContext<TIncoming>(message, timeout, isKeepAlive, cancellationToken);

            _requestContexts.TryAdd(message.RequestId, requestContext);

            switch (type)
            {
                case MessageType.Request:
                case MessageType.Response:
                case MessageType.Fault:
                    try
                    {
                        await connection.SendAsync(message, cancellationToken);

                        return await requestContext.CompletionTask;
                    }
                    finally
                    {
                        RemoveRequestContext(message.RequestId);
                    }

                default:
                    break;
            }

            return null;
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return;
            }

            RequestContext requestContext;

            if (_requestContexts.TryGetValue(e.Message.RequestId, out requestContext))
            {
                switch (e.Message.Type)
                {
                    case MessageType.Response:
                        requestContext.HandleResponse(e.Message);
                        break;

                    case MessageType.Progress:
                        requestContext.HandleProgress(e.Message);
                        break;

                    case MessageType.Fault:
                        requestContext.HandleFault(e.Message);
                        break;

                    case MessageType.Cancel:
                        requestContext.HandleCancel();
                        break;

                    default:
                        throw new ProtocolException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Plugin_UnrecognizedMessageType,
                                e.Message.Type));
                }

                return;
            }

            switch (e.Message.Type)
            {
                case MessageType.Request:
                    HandleInboundRequest(connection, e.Message);
                    break;

                case MessageType.Fault:
                    break;

                default:
                    throw new ProtocolException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Plugin_UnrecognizedMessageType,
                            e.Message.Type));
            }
        }

        private void HandleInboundRequest(IConnection connection, Message message)
        {
            var requestHandler = GetInboundRequestHandler(message.Method);
            var requestContext = CreateRequestContext<HandshakeResponse>(
                message,
                connection.Options.HandshakeTimeout,
                isKeepAlive: false,
                cancellationToken: requestHandler.CancellationToken);

            _requestContexts.TryAdd(message.RequestId, requestContext);

            requestContext.BeginResponseAsync(message, requestHandler, this);
        }

        private void HandleInboundProgress(Message message)
        {
            var requestHandler = GetInboundRequestHandler(message.Method);
            var requestContext = GetRequestContext(message.RequestId);

            requestContext.BeginProgressAsync(message, requestHandler);
        }

        private IRequestHandler GetInboundRequestHandler(MessageMethod method)
        {
            IRequestHandler handler;

            if (!RequestHandlers.TryGet(method, out handler))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestHandlerDoesNotExist, method));
            }

            return handler;
        }

        private RequestContext GetRequestContext(string requestId)
        {
            RequestContext requestContext;

            if (!_requestContexts.TryGetValue(requestId, out requestContext))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestContextDoesNotExist, requestId));
            }

            return requestContext;
        }

        private void RemoveRequestContext(string requestId)
        {
            RequestContext requestContext;

            _requestContexts.TryRemove(requestId, out requestContext);
        }

        private static RequestContext<TOutgoing> CreateRequestContext<TOutgoing>(
            Message message,
            TimeSpan? timeout,
            bool isKeepAlive,
            CancellationToken cancellationToken)
        {
            return new RequestContext<TOutgoing>(
                message.RequestId,
                timeout,
                isKeepAlive,
                cancellationToken);
        }

        private static bool GetIsKeepAlive(IConnection connection, MessageType type, MessageMethod method)
        {
            if (type == MessageType.Request && method == MessageMethod.Handshake)
            {
                return false;
            }

            return true;
        }

        private static TimeSpan GetRequestTimeout(IConnection connection, MessageType type, MessageMethod method)
        {
            if (type == MessageType.Request && method == MessageMethod.Handshake)
            {
                return connection.Options.HandshakeTimeout;
            }

            return connection.Options.RequestTimeout;
        }

        private abstract class RequestContext : IDisposable
        {
            internal string RequestId { get; }

            public RequestContext(string requestId)
            {
                RequestId = requestId;
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool disposing);

            public abstract void BeginProgressAsync(Message message, IRequestHandler requestHandler);
            public abstract void HandleProgress(Message message);
            public abstract void BeginResponseAsync(
                Message message,
                IRequestHandler requestHandler,
                IResponseHandler responseHandler);
            public abstract void HandleResponse(Message message);
            public abstract void HandleFault(Message message);
            public abstract void HandleCancel();
        }

        private sealed class RequestContext<TResult> : RequestContext
        {
            private readonly CancellationTokenSource _timeoutCancellationTokenSource;
            private readonly CancellationTokenSource _combinedCancellationTokenSource;
            private bool _isDisposed;
            private bool _isKeepAlive;
            private readonly TimeSpan? _timeout;
            private Task _responseTask;
            private readonly TaskCompletionSource<TResult> _taskCompletionSource;

            internal Task<TResult> CompletionTask => _taskCompletionSource.Task;

            internal RequestContext(
                string requestId,
                TimeSpan? timeout,
                bool isKeepAlive,
                CancellationToken cancellationToken)
                : base(requestId)
            {
                _taskCompletionSource = new TaskCompletionSource<TResult>();
                _timeout = timeout;
                _isKeepAlive = isKeepAlive;

                if (timeout.HasValue)
                {
                    _timeoutCancellationTokenSource = new CancellationTokenSource(timeout.Value);
                    _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        _timeoutCancellationTokenSource.Token,
                        cancellationToken);
                }
                else
                {
                    _combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                _combinedCancellationTokenSource.Token.Register(Close);
            }

            public override void BeginProgressAsync(Message message, IRequestHandler requestHandler)
            {
                Task.Factory.StartNew(
                    () => requestHandler.HandleProgressAsync(message, _combinedCancellationTokenSource.Token),
                        _combinedCancellationTokenSource.Token);
            }

            public override void HandleProgress(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<Progress>(message);

                if (_timeout.HasValue && _isKeepAlive)
                {
                    _timeoutCancellationTokenSource.CancelAfter(_timeout.Value);
                }
            }

            public override void BeginResponseAsync(
                Message message,
                IRequestHandler requestHandler,
                IResponseHandler responseHandler)
            {
                _responseTask = Task.Factory.StartNew(
                    () => requestHandler.HandleResponseAsync(
                            message,
                            responseHandler,
                            _combinedCancellationTokenSource.Token),
                        _combinedCancellationTokenSource.Token);
            }

            public override void HandleResponse(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<TResult>(message);

                try
                {
                    _taskCompletionSource.SetResult(payload);
                }
                catch (Exception ex)
                {
                    _taskCompletionSource.TrySetException(ex);
                }
            }

            public override void HandleFault(Message message)
            {
            }

            public override void HandleCancel()
            {
                _combinedCancellationTokenSource.Cancel();
            }

            protected override void Dispose(bool disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    Close();
                }

                _isDisposed = true;
            }

            private void Close()
            {
                _taskCompletionSource.TrySetCanceled();

                if (_timeoutCancellationTokenSource != null)
                {
                    using (_timeoutCancellationTokenSource)
                    {
                        _timeoutCancellationTokenSource.Cancel();
                    }
                }

                using (_combinedCancellationTokenSource)
                {
                    _combinedCancellationTokenSource.Cancel();
                }
            }
        }
    }
}