// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private readonly BlockingCollection<MessageContext> _sendQueue;
        private Task<Task> _sendThread;
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
            _sendQueue = new BlockingCollection<MessageContext>();
            _cancellationTokenSource = new CancellationTokenSource();
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

            _sendQueue.CompleteAdding();

            using (_cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_sendThread != null)
            {
                // Wait for the thread to exit.
                _sendThread.GetAwaiter().GetResult();
            }

            foreach (var messageContext in _sendQueue)
            {
                messageContext.CompletionSource.TrySetCanceled();
            }

            _sendQueue.Dispose();
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

            if (_sendThread != null)
            {
                throw new InvalidOperationException(Strings.Plugin_ConnectionAlreadyStarted);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _sendThread = Task.Factory.StartNew(SendAsync, cancellationToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

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
        public async Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var messageContext = new MessageContext(message);

            cancellationToken.Register(() => messageContext.CompletionSource.TrySetCanceled());

            _sendQueue.Add(messageContext);

            await messageContext.CompletionSource.Task;
        }

        private Task SendAsync()
        {
            try
            {
                using (var jsonWriter = new JsonTextWriter(_textWriter))
                {
                    jsonWriter.CloseOutput = false;

                    foreach (var messageContext in _sendQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        try
                        {
                            JsonSerializationUtilities.Serialize(jsonWriter, messageContext.Message);

                            // We need to terminate JSON objects with a delimiter (i.e.:  a single
                            // newline sequence) to signal to the receiver when to stop reading.
                            _textWriter.WriteLine();
                            _textWriter.Flush();

                            messageContext.CompletionSource.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            messageContext.CompletionSource.TrySetException(ex);
                        }
                    }
                }
            }
            catch (Exception)
            {
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

        private sealed class MessageContext
        {
            internal TaskCompletionSource<bool> CompletionSource { get; }
            internal Message Message { get; }

            internal MessageContext(Message message)
            {
                Message = message;
                CompletionSource = new TaskCompletionSource<bool>();
            }
        }
    }
}