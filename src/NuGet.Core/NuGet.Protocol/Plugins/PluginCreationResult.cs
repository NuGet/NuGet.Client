// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A plugin's creation result.
    /// </summary>
    public sealed class PluginCreationResult
    {
        /// <summary>
        /// Gets the plugin's operation claims.
        /// </summary>
        public IReadOnlyList<OperationClaim> Claims { get; }

        /// <summary>
        /// Gets a message if <see cref="Plugin" /> is <c>null</c>; otherwise, <c>null</c>.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets a plugin.
        /// </summary>
        public IPlugin Plugin { get; }

        /// <summary>
        /// Instantiates a new <see cref="PluginCreationResult" /> class.
        /// </summary>
        /// <param name="plugin">A plugin.</param>
        /// <param name="claims">The plugin's operation claims.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plugin" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="claims" /> is <c>null</c>.</exception>
        public PluginCreationResult(IPlugin plugin, IReadOnlyList<OperationClaim> claims)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            Plugin = plugin;
            Claims = claims;
        }

        /// <summary>
        /// Instantiates a new <see cref="PluginCreationResult" /> class.
        /// </summary>
        /// <param name="message">A message why a plugin could not be created.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="message" />
        /// is either <c>null</c> or an empty string.</exception>
        public PluginCreationResult(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(message));
            }

            Message = message;
        }
    }
}