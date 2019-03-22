// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Context for an inbound request.
    /// </summary>
    public sealed class InboundRequestContext : IDisposable
    {
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IConnection _connection;
        private bool _isDisposed;
        private readonly IPluginLogger _logger;

        /// <summary>
        /// Gets the request ID.
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// Initializes a new <see cref="InboundRequestContext" /> class.
        /// </summary>
        /// <param name="connection">A connection.</param>
        /// <param name="requestId">A request ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="requestId" />
        /// is either <c>null</c> or an empty string.</exception>
        public InboundRequestContext(
            IConnection connection,
            string requestId,
            CancellationToken cancellationToken)
            : this(connection, requestId, cancellationToken, PluginLogger.DefaultInstance)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="InboundRequestContext" /> class.
        /// </summary>
        /// <param name="connection">A connection.</param>
        /// <param name="requestId">A request ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="logger">A plugin logger.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="connection" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="requestId" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" />
        /// is <c>null</c>.</exception>
        internal InboundRequestContext(
            IConnection connection,
            string requestId,
            CancellationToken cancellationToken,
            IPluginLogger logger)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (string.IsNullOrEmpty(requestId))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(requestId));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _connection = connection;
            RequestId = requestId;

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Capture the cancellation token now because if the cancellation token source
            // is disposed race conditions may cause an exception acccessing its Token property.
            _cancellationToken = _cancellationTokenSource.Token;

            _logger = logger;
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

            try
            {
                using (_cancellationTokenSource)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
            catch (Exception)
            {
            }

            // Do not dispose of _connection or _logger.  This context does not own them.

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously starts processing a fault response for the inbound request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="exception">An exception.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" />
        /// is either <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception" />
        /// is <c>null</c>.</exception>
        public void BeginFaultAsync(Message request, Exception exception)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var responsePayload = new Fault(exception.Message);
            var response = new Message(
                request.RequestId,
                MessageType.Fault,
                request.Method,
                JsonSerializationUtilities.FromObject(responsePayload));

            if (_logger.IsEnabled)
            {
                _logger.Write(new TaskLogMessage(_logger.Now, response.RequestId, response.Method, response.Type, TaskState.Queued));
            }

            Task.Run(async () =>
                {
                    // Top-level exception handler for a worker pool thread.
                    try
                    {
                        if (_logger.IsEnabled)
                        {
                            _logger.Write(new TaskLogMessage(_logger.Now, response.RequestId, response.Method, response.Type, TaskState.Executing));
                        }

                        await _connection.SendAsync(response, _cancellationToken);
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        if (_logger.IsEnabled)
                        {
                            _logger.Write(new TaskLogMessage(_logger.Now, response.RequestId, response.Method, response.Type, TaskState.Completed));
                        }
                    }
                },
                _cancellationToken);
        }

        /// <summary>
        /// Asynchronously starts processing a response for the inbound request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestHandler">A request handler.</param>
        /// <param name="responseHandler">A response handler.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="requestHandler" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="responseHandler" />
        /// is <c>null</c>.</exception>
        public void BeginResponseAsync(
            Message request,
            IRequestHandler requestHandler,
            IResponseHandler responseHandler)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (requestHandler == null)
            {
                throw new ArgumentNullException(nameof(requestHandler));
            }

            if (responseHandler == null)
            {
                throw new ArgumentNullException(nameof(responseHandler));
            }

            if (_logger.IsEnabled)
            {
                _logger.Write(new TaskLogMessage(_logger.Now, request.RequestId, request.Method, request.Type, TaskState.Queued));
            }

            Task.Run(async () =>
                {
                    // Top-level exception handler for a worker pool thread.
                    try
                    {
                        if (_logger.IsEnabled)
                        {
                            _logger.Write(new TaskLogMessage(_logger.Now, request.RequestId, request.Method, request.Type, TaskState.Executing));
                        }

                        await requestHandler.HandleResponseAsync(
                            _connection,
                            request,
                            responseHandler,
                            _cancellationToken);
                    }
                    catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                    {
                        var response = MessageUtilities.Create(request.RequestId, MessageType.Cancel, request.Method);

                        await _connection.SendAsync(response, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        BeginFaultAsync(request, ex);
                    }
                    finally
                    {
                        if (_logger.IsEnabled)
                        {
                            _logger.Write(new TaskLogMessage(_logger.Now, request.RequestId, request.Method, request.Type, TaskState.Completed));
                        }
                    }
                },
                _cancellationToken);
        }

        /// <summary>
        /// Cancels an inbound request.
        /// </summary>
        public void Cancel()
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}