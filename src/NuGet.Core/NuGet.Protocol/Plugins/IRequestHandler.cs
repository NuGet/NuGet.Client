// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a request handler.
    /// </summary>
    public interface IRequestHandler
    {
        /// <summary>
        /// Gets the <see cref="CancellationToken" /> for a request.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Asynchronously handles cancelling a request.
        /// </summary>
        /// <param name="request">A request message.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        /// <exception cref="NotSupportedException">Thrown if cancellation is not supported.</exception>
        Task HandleCancelAsync(Message request, CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously handles progress notifications for a request.
        /// </summary>
        /// <param name="request">A request message.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        /// <exception cref="NotSupportedException">Thrown if progress notification are not supported.</exception>
        Task HandleProgressAsync(Message request, CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously handles responding to a request.
        /// </summary>
        /// <param name="request">A request message.</param>
        /// <param name="responseHandler">A response handler.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="responseHandler" />
        /// is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        Task HandleResponseAsync(Message request, IResponseHandler responseHandler, CancellationToken cancellationToken);
    }
}