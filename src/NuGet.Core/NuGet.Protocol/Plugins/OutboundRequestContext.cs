// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Context for an outbound request.
    /// </summary>
    public abstract class OutboundRequestContext : IDisposable
    {
        /// <summary>
        /// Gets the request ID.
        /// </summary>
        public string RequestId { get; protected set; }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Handles cancellation for the outbound request.
        /// </summary>
        public abstract void HandleCancel();

        /// <summary>
        /// Handles progress notifications for the outbound request.
        /// </summary>
        /// <param name="progress">A progress notification.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="progress" /> is <c>null</c>.</exception>
        public abstract void HandleProgress(Message progress);

        /// <summary>
        /// Handles a response for the outbound request.
        /// </summary>
        /// <param name="response">A response.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="response" /> is <c>null</c>.</exception>
        public abstract void HandleResponse(Message response);

        /// <summary>
        /// Handles a fault response for the outbound request.
        /// </summary>
        /// <param name="fault">A fault response.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fault" /> is <c>null</c>.</exception>
        public abstract void HandleFault(Message fault);
    }
}