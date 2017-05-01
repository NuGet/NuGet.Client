// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Represents a unidirectional communications channel from a target.
    /// </summary>
    public abstract class Receiver : IReceiver
    {
        /// <summary>
        /// Gets or sets a flag indicating whether or not this instance is disposed.
        /// </summary>
        protected bool IsDisposed { get; set; }

        /// <summary>
        /// Occurs when an unrecoverable fault has been caught.
        /// </summary>
        public event EventHandler<ProtocolErrorEventArgs> Faulted;

        /// <summary>
        /// Occurs when a message has been received.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// Asynchronously closes the connection.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        public abstract Task CloseAsync();

        /// <summary>
        /// Asynchronously connects.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public abstract Task ConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public abstract void Dispose();

        protected void FireFaultEvent(Exception exception, Message message)
        {
            var ex = new ProtocolException(Strings.Plugin_ProtocolException, exception);
            var eventArgs = message == null
                ? new ProtocolErrorEventArgs(ex) : new ProtocolErrorEventArgs(ex, message);

            Faulted?.Invoke(this, eventArgs);
        }

        protected void FireMessageReceivedEvent(Message message)
        {
            MessageReceived?.Invoke(this, new MessageEventArgs(message));
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}