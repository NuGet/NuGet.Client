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
        private int _lock;

        private ILogger _delegateLogger;

        internal DelegatingLogger(ILogger delegateLogger)
        {
            _delegateLogger = delegateLogger ?? throw new ArgumentNullException(nameof(delegateLogger));
        }

        internal void UpdateDelegate(ILogger logger)
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) == 0)
            {
                _delegateLogger = logger ?? throw new ArgumentNullException(nameof(logger));
            }
        }

        public override void Log(ILogMessage message)
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) == 0)
            {
                _delegateLogger?.Log(message);
            }
        }

        public override async Task LogAsync(ILogMessage message)
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) == 0)
            {
                await _delegateLogger?.LogAsync(message);
            }
        }
    }
}
