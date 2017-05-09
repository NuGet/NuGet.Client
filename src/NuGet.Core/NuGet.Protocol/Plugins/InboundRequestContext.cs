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
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (string.IsNullOrEmpty(requestId))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(requestId));
            }

            _connection = connection;
            RequestId = requestId;

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Capture the cancellation token now because if the cancellation token source
            // is disposed race conditions may cause an exception acccessing its Token property.
            _cancellationToken = _cancellationTokenSource.Token;
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

            // Do not dispose of _connection.

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }


        /// <summary>
        /// Asynchronously starts processing a cancellation request for the inbound request.
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
        public void BeginCancelAsync(
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

            Task.Run(async () =>
                {
                    // Top-level exception handler for a worker pool thread.
                    try
                    {
                        await requestHandler.HandleCancelAsync(
                            _connection,
                            request,
                            responseHandler,
                            _cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        BeginFaultAsync(request, ex);
                    }
                },
                _cancellationToken);
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

            Task.Run(async () =>
                {
                    // Top-level exception handler for a worker pool thread.
                    try
                    {
                        await _connection.SendAsync(response, _cancellationToken);
                    }
                    catch (Exception)
                    {
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

            Task.Run(async () =>
                {
                    // Top-level exception handler for a worker pool thread.
                    try
                    {
                        await requestHandler.HandleResponseAsync(
                            _connection,
                            request,
                            responseHandler,
                            _cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        BeginFaultAsync(request, ex);
                    }
                },
                _cancellationToken);
        }
    }
}