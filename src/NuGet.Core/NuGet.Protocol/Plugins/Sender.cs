// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a unidirectional communications channel to a target.
    /// </summary>
    /// <remarks>
    /// Any public static members of this type are thread safe.
    /// Any instance members are not guaranteed to be thread safe.
    /// </remarks>
    public sealed class Sender : ISender
    {
        private bool _isConnected;
        private bool _isDisposed;
        private readonly object _sendLock;
        private readonly TextWriter _textWriter;

        /// <summary>
        /// Instantiates a new <see cref="Sender" /> class.
        /// </summary>
        /// <param name="writer">A text writer.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="writer" /> is <c>null</c>.</exception>
        public Sender(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _textWriter = writer;
            _sendLock = new object();
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

            _textWriter.Dispose();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously closes the connection.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        public Task CloseAsync()
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
        /// <exception cref="InvalidOperationException">Thrown if this method has already been called.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_isConnected)
            {
                throw new InvalidOperationException(Strings.Plugin_ConnectionAlreadyStarted);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _isConnected = true;

            return Task.FromResult(0);
        }

        /// <summary>
        /// Asynchronously sends a message to the target.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock(_sendLock)
            {
                using (var jsonWriter = new JsonTextWriter(_textWriter))
                {
                    jsonWriter.CloseOutput = false;

                    JsonSerializationUtilities.Serialize(jsonWriter, message);

                    // We need to terminate JSON objects with a delimiter (i.e.:  a single
                    // newline sequence) to signal to the receiver when to stop reading.
                    _textWriter.WriteLine();
                    _textWriter.Flush();
                }
            }

            return Task.FromResult(0);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(Sender));
            }
        }
    }
}