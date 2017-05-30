// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a plugin process.
    /// </summary>
    public sealed class PluginProcess : IPluginProcess
    {
        private bool _isDisposed;
        private readonly Process _process;

        /// <summary>
        /// Occurs when a process exits.
        /// </summary>
        public event EventHandler Exited
        {
            add
            {
                _process.Exited += value;
            }
            remove
            {
                _process.Exited -= value;
            }
        }

        /// <summary>
        /// Occurs when a line of output has been received.
        /// </summary>
        public event EventHandler<LineReadEventArgs> LineRead;

        /// <summary>
        /// Gets a value indicating whether the associated process has been terminated.
        /// </summary>
        public bool HasExited
        {
            get
            {
                return _process.HasExited;
            }
        }

        /// <summary>
        /// Instantiates a new <see cref="PluginProcess" /> class.
        /// </summary>
        /// <param name="process">A plugin process.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="process" /> is <c>null</c>.</exception>
        public PluginProcess(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            _process = process;
            _process.OutputDataReceived += OnOutputDataReceived;
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.Dispose();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously starts reading the standard output stream.
        /// </summary>
        public void BeginReadLine()
        {
            _process.BeginOutputReadLine();
        }

        /// <summary>
        /// Cancels asynchronous reading of the standard output stream.
        /// </summary>
        public void CancelRead()
        {
            _process.CancelOutputRead();
        }

        /// <summary>
        /// Immediately stops the associated process.
        /// </summary>
        public void Kill()
        {
            try
            {
                if (_process.HasExited)
                {
                    return;
                }

                _process.Kill();
            }
            catch (Exception)
            {
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            LineRead?.Invoke(sender, new LineReadEventArgs(e.Data));
        }
    }
}