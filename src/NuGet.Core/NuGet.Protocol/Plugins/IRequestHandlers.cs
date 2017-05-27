﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A set of <see cref="IRequestHandler" />.
    /// </summary>
    public interface IRequestHandlers
    {
        /// <summary>
        /// Attempts to add a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <param name="handler">A request handler.</param>
        /// <returns><c>true</c> if added; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler" /> is <c>null</c>.</exception>
        bool TryAdd(MessageMethod method, IRequestHandler handler);

        /// <summary>
        /// Attempts to get a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <param name="handler">A request handler.</param>
        /// <returns><c>true</c> if the request handler exists; otherwise, <c>false</c>.</returns>
        bool TryGet(MessageMethod method, out IRequestHandler handler);

        /// <summary>
        /// Attempts to remove a request handler for the specified message method.
        /// </summary>
        /// <param name="method">A message method.</param>
        /// <returns><c>true</c> if a request handler was removed; otherwise, <c>false</c>.</returns>
        bool TryRemove(MessageMethod method);
    }
}