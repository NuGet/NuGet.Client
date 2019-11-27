// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    internal class InboundRequestProcessingHandler : IDisposable
    {
        private readonly ISet<MessageMethod> _fastProccessingMethods;
        private readonly Lazy<DedicatedAsynchronousProcessingThread> _processingThread;
        private bool _isDisposed;

        public InboundRequestProcessingHandler() :
            this(new HashSet<MessageMethod>())
        {
        }

        public InboundRequestProcessingHandler(ISet<MessageMethod> fastProcessingMethods) 
        {
            _fastProccessingMethods = fastProcessingMethods ?? throw new ArgumentNullException(nameof(fastProcessingMethods));
            // Lazily initialize the processing thread. It is not needed if there are no time critical methods.
            _processingThread = new Lazy<DedicatedAsynchronousProcessingThread>(() =>
            {
                var thread = new DedicatedAsynchronousProcessingThread();
                thread.Start();
                return thread;
            });

        }

        internal void Handle(MessageMethod messageMethod, Func<Task> task, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (_fastProccessingMethods.Contains(messageMethod))
            {
                _processingThread.Value.Push(task);
            }
            else
            {
                Task.Run(async () =>
                {
                    await task();
                }, cancellationToken);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _processingThread.Value.Dispose();
            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
