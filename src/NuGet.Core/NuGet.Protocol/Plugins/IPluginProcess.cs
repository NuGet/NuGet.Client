// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a plugin process.
    /// </summary>
    public interface IPluginProcess : IDisposable
    {
        /// <summary>
        /// Occurs when a line of output has been received.
        /// </summary>
        event EventHandler<LineReadEventArgs> LineRead;

        /// <summary>
        /// Occurs when a process exits.
        /// </summary>
        event EventHandler Exited;

        /// <summary>
        /// Gets a value indicating whether the associated process has been terminated.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Asynchronously starts reading the standard output stream.
        /// </summary>
        void BeginReadLine();

        /// <summary>
        /// Cancels asynchronous reading of the standard output stream.
        /// </summary>
        void CancelRead();

        /// <summary>
        /// Immediately stops the associated process.
        /// </summary>
        void Kill();
    }
}