﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A response handler.
    /// </summary>
    public interface IResponseHandler
    {
        /// <summary>
        /// Asynchronously handles cancelling a request.
        /// </summary>
        /// <typeparam name="TPayload">The response payload type.</typeparam>
        /// <param name="request">A request message.</param>
        /// <param name="payload">The response payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        Task SendResponseAsync<TPayload>(Message request, TPayload payload, CancellationToken cancellationToken)
            where TPayload : class;
    }
}