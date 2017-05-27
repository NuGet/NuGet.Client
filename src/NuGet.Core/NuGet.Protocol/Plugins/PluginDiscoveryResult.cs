// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A plugin discovery result.
    /// </summary>
    public sealed class PluginDiscoveryResult
    {
        /// <summary>
        /// Gets the plugin file.
        /// </summary>
        public PluginFile PluginFile { get; }

        /// <summary>
        /// Gets a message if <see cref="PluginFile.State" /> is not <see cref="PluginFileState.Valid" />;
        /// otherwise, <c>null</c>.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Instantiates a new <see cref="PluginDiscoveryResult" /> class.
        /// </summary>
        /// <param name="pluginFile">A plugin file.</param>
        /// <param name="message">A message if <see cref="PluginFile.State" /> is not
        /// <see cref="PluginFileState.Valid" />; otherwise, <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginFile" />
        /// is <c>null</c>.</exception>
        public PluginDiscoveryResult(PluginFile pluginFile, string message = null)
        {
            if (pluginFile == null)
            {
                throw new ArgumentNullException(nameof(pluginFile));
            }

            PluginFile = pluginFile;
            Message = message;
        }
    }
}