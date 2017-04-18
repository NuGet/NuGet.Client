// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a plugin.
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>
        /// Gets the connection for the plugin.
        /// </summary>
        IConnection Connection { get; }

        /// <summary>
        /// Gets the file path for the plugin.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        string Name { get; }
    }
}