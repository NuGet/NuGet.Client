// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a unidirectional communications channel from a target.
    /// </summary>
    /// <remarks>
    /// Any public static members of this type are thread safe.
    /// Any instance members are not guaranteed to be thread safe.
    /// </remarks>
    public sealed class StandardOutputReceiver : Receiver
    {
        private bool _detectUtf8Bom;
        private bool _isConnected;
        private readonly IPluginProcess _process;

        /// <summary>
        /// Instantiates a new <see cref="StandardOutputReceiver" /> class.
        /// </summary>
        /// <param name="process">A plugin process.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="process" /> is <c>null</c>.</exception>
        public StandardOutputReceiver(IPluginProcess process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            _process = process;
            _detectUtf8Bom = true;
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public override void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            _process.LineRead -= OnLineRead;

            _process.CancelRead();

            // The process instance is shared with other classes and will be disposed elsewhere.

            GC.SuppressFinalize(this);

            IsDisposed = true;
        }

        /// <summary>
        /// Asynchronously closes the connection.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        public override Task CloseAsync()
        {
            ThrowIfDisposed();

            Dispose();

            return Task.FromResult(0);
        }

        /// <summary>
        /// Asynchronously connects.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override Task ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_isConnected)
            {
                throw new InvalidOperationException(Strings.Plugin_ConnectionAlreadyStarted);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _process.LineRead += OnLineRead;
            _process.BeginReadLine();

            _isConnected = true;

            return Task.FromResult(0);
        }

        private void OnLineRead(object sender, LineReadEventArgs e)
        {
            // Top-level exception handler for a worker pool thread.
            try
            {
                string json;

                if (_detectUtf8Bom)
                {
                    json = RemoveUtf8Bom(e.Line);

                    _detectUtf8Bom = false;
                }
                else
                {
                    json = e.Line;
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var message = JsonSerializationUtilities.Deserialize<Message>(json);

                    if (message != null)
                    {
                        FireMessageReceivedEventAndForget(message);
                    }
                }
            }
            catch (Exception ex)
            {
                FireFaultEventAndForget(ex);
            }
        }

        private static string RemoveUtf8Bom(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                var bytes = Encoding.UTF8.GetBytes(data);

                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    return Encoding.UTF8.GetString(bytes, index: 3, count: bytes.Length - 3);
                }
            }

            return data;
        }
    }
}