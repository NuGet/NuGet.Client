// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Credentials
{
    /// <summary>
    ///  A delegating logger.
    /// </summary>
    internal class DelegatingLogger : LoggerBase, ILogger
    {
        private readonly SemaphoreSlim _semaphore;
        private ILogger _delegateLogger;

        internal DelegatingLogger(ILogger delegateLogger) : base()
        {
            _delegateLogger = delegateLogger ?? throw new ArgumentNullException(nameof(delegateLogger));
            _semaphore = new SemaphoreSlim(1, 1);
        }

        internal void UpdateDelegate(ILogger logger)
        {
            _semaphore.Wait();
            try
            {
                _delegateLogger = logger ?? throw new ArgumentNullException(nameof(logger));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override void Log(ILogMessage message)
        {
            _semaphore.Wait();
            try
            {
                _delegateLogger?.Log(message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override async Task LogAsync(ILogMessage message)
        {
            await _semaphore.WaitAsync();
            try
            {
                await _delegateLogger?.LogAsync(message);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
