// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A set of <see cref="IRequestHandler" />.
    /// </summary>
    public sealed class RequestHandlers : IRequestHandlers
    {
        private readonly ConcurrentDictionary<MessageMethod, IRequestHandler> _handlers;

        /// <summary>
        /// Instantiates a new <see cref="RequestHandlers" /> class.
        /// </summary>
        public RequestHandlers()
        {
            _handlers = new ConcurrentDictionary<MessageMethod, IRequestHandler>();
        }

        /// <summary>
        /// Attempts to add a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <param name="handler">A request handler.</param>
        /// <returns><c>true</c> if added; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler" /> is <c>null</c>.</exception>
        public bool TryAdd(MessageMethod method, IRequestHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return _handlers.TryAdd(method, handler);
        }

        /// <summary>
        /// Attempts to get a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <param name="handler">An existing request handler.</param>
        /// <returns><c>true</c> if the request handler exists; otherwise, <c>false</c>.</returns>
        public bool TryGet(MessageMethod method, out IRequestHandler handler)
        {
            return _handlers.TryGetValue(method, out handler);
        }

        /// <summary>
        /// Attempts to remove a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <returns><c>true</c> if a request handler was removed; otherwise, <c>false</c>.</returns>
        public bool TryRemove(MessageMethod method)
        {
            IRequestHandler handler;

            return _handlers.TryRemove(method, out handler);
        }
    }
}