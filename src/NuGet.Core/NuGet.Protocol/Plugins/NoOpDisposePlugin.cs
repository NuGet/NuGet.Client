// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A plugin wrapper that no-ops IDisposable.
    /// </summary>
    public sealed class NoOpDisposePlugin : IPlugin
    {
        private readonly IPlugin _plugin;

        /// <summary>
        /// Gets the connection for the plugin.
        /// </summary>
        public IConnection Connection => _plugin.Connection;

        /// <summary>
        /// Gets the file path for the plugin.
        /// </summary>
        public string FilePath => _plugin.FilePath;

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public string Name => _plugin.Name;

        /// <summary>
        /// Instantiates a new <see cref="NoOpDisposePlugin" /> class.
        /// </summary>
        /// <param name="plugin">A plugin</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plugin" /> is <c>null</c>.</exception>
        public NoOpDisposePlugin(IPlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            _plugin = plugin;
        }

        public void Dispose()
        {
        }
    }
}