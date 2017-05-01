// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
    public sealed class StandardInputReceiver : Receiver
    {
        private readonly CancellationTokenSource _receiveCancellationTokenSource;
        private readonly TextReader _reader;
        private Task _receiveThread;

        /// <summary>
        /// Instantiates a new <see cref="StandardInputReceiver" /> class.
        /// </summary>
        /// <param name="reader">A text reader.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="reader" /> is <c>null</c>.</exception>
        public StandardInputReceiver(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            _reader = reader;
            _receiveCancellationTokenSource = new CancellationTokenSource();
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

            using (_receiveCancellationTokenSource)
            {
                _receiveCancellationTokenSource.Cancel();

                // Do not attempt to wait on completion of the receive thread task.
                // In scenarios where standard input is backed by a non-blocking stream
                // (e.g.:  a MemoryStream in unit tests) waiting on the receive thread task
                // is fine.  However, when standard input is backed by a blocking stream,
                // reading from standard input is a blocking call, and while the receive
                // thread is in a read call it cannot respond to cancellation requests.
                // We would likely hang if we attempted to wait on completion of the
                // receive thread task.
            }

            _reader.Dispose();

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

            if (_receiveThread != null)
            {
                throw new InvalidOperationException(Strings.Plugin_ConnectionAlreadyStarted);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _receiveThread = Task.Factory.StartNew(
                Receive,
                _receiveCancellationTokenSource.Token,
                cancellationToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            return Task.FromResult(0);
        }

        private void Receive(object state)
        {
            Message message = null;

            try
            {
                var cancellationToken = (CancellationToken)state;

                string line;

                // Reading from the standard input stream is a blocking call; while we're
                // in a read call we can't respond to cancellation requests.
                while ((line = _reader.ReadLine()) != null)
                {
                    message = null;

                    cancellationToken.ThrowIfCancellationRequested();

                    message = JsonSerializationUtilities.Deserialize<Message>(line);

                    if (message != null)
                    {
                        FireMessageReceivedEvent(message);
                    }
                }
            }
            catch (Exception ex)
            {
                FireFaultEvent(ex, message);
            }
        }
    }
}