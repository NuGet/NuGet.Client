// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public class CollectorLogger : LoggerBase, ICollectorLogger
    {
        private readonly ILogger _innerLogger;
        private readonly ConcurrentQueue<ILogMessage> _errors;

        /// <summary>
        /// Initializes an instance of the <see cref="CollectorLogger"/>, while still
        /// delegating all log messages to the <param name="innerLogger" />.
        /// </summary>
        public CollectorLogger(ILogger innerLogger)
        {
            _innerLogger = innerLogger;
            _errors = new ConcurrentQueue<ILogMessage>();
        }

        public override void Log(ILogMessage message)
        {
            _innerLogger.Log(message);
            _errors.Enqueue(message);
        }

        public async override Task LogAsync(ILogMessage message)
        {
            _innerLogger.Log(message);
            _errors.Enqueue(message);
        }

        public new void LogDebug(string data)
        {
            _innerLogger.LogDebug(data);
        }

        public new void LogVerbose(string data)
        {
            _innerLogger.LogVerbose(data);
        }

        public new void LogInformation(string data)
        {
            _innerLogger.LogInformation(data);
        }

        public new void LogMinimal(string data)
        {
            _innerLogger.LogMinimal(data);
        }

        public IEnumerable<ILogMessage> Errors => _errors.ToArray();
    }
}
