// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a plugin file.
    /// </summary>
    public sealed class PluginFile
    {
        /// <summary>
        /// Gets the plugin's file path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the plugin file state.
        /// </summary>
        public Lazy<PluginFileState> State { get; }

        /// <summary>
        /// Is the plugin file, a dotnet tools plugin file?
        /// </summary>
        internal bool IsDotnetToolsPlugin { get; }

        /// <summary>
        /// Instantiates a new <see cref="PluginFile" /> class.
        /// </summary>
        /// <param name="filePath">The plugin's file path.</param>
        /// <param name="state">A lazy that evaluates the plugin file state.</param>
        /// <param name="isDotnetToolsPlugin">Is the plugin file, a dotnet tools plugin file?</param>
        internal PluginFile(string filePath, Lazy<PluginFileState> state, bool isDotnetToolsPlugin) : this(filePath, state)
        {
            IsDotnetToolsPlugin = isDotnetToolsPlugin;
        }

        /// <summary>
        /// Instantiates a new <see cref="PluginFile" /> class.
        /// </summary>
        /// <param name="filePath">The plugin's file path.</param>
        /// <param name="state">A lazy that evaluates the plugin file state.</param>
        public PluginFile(string filePath, Lazy<PluginFileState> state)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            Path = filePath;
            State = state;
        }

        public override string ToString()
        {
            return $"{Path} : {State}";
        }
    }
}
