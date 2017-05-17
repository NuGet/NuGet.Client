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
        private readonly ConcurrentQueue<IRestoreLogMessage> _errors;
        private readonly bool _displayAllLogs;

        /// <summary>
        /// Initializes an instance of the <see cref="CollectorLogger"/>, while still
        /// delegating all log messages to the <param name="innerLogger" />
        /// based on the <param name="verbosity" />
        /// </summary>
        public CollectorLogger(ILogger innerLogger, LogLevel verbosity, bool displayAllLogs)
            : base(verbosity)
        {
            _innerLogger = innerLogger;
            _errors = new ConcurrentQueue<IRestoreLogMessage>();
            _displayAllLogs = displayAllLogs;
        }

        /// <summary>
        /// Initializes an instance of the <see cref="CollectorLogger"/>, while still
        /// delegating all log messages to the <param name="innerLogger" />.
        /// </summary>
        public CollectorLogger(ILogger innerLogger)
            : this(innerLogger, LogLevel.Verbose, true)
        {
        }


        public void Log(IRestoreLogMessage message)
        {
            if (CollectMessage(message.Level))
            {
                _errors.Enqueue(message);
            }

            if (DisplayMessage(message))
            {
                _innerLogger.Log(message);
            }
        }

        public Task LogAsync(IRestoreLogMessage message)
        {
            if (CollectMessage(message.Level))
            {
                _errors.Enqueue(message);
            }

            if (DisplayMessage(message))
            {
                return _innerLogger.LogAsync(message);
            }
            else
            {
                return Task.FromResult(0);
            }
        }
        public override void Log(ILogMessage message)
        {
            Log(ToRestoreLogMessage(message));
        }

        public override Task LogAsync(ILogMessage message)
        {
            return LogAsync(ToRestoreLogMessage(message));
        }

        /// <summary>
        /// Decides if the log should be passed to the inner logger.
        /// </summary>
        /// <param name="message">IRestoreLogMessage to be logged.</param>
        /// <returns>bool indicating if this message should be logged.</returns>
        protected bool DisplayMessage(IRestoreLogMessage message)
        {
            if (message.Level == LogLevel.Error || message.Level == LogLevel.Warning)
            {
                return ((_displayAllLogs || message.ShouldDisplay) && message.Level >= VerbosityLevel);
            }
            else
            {
                return (message.Level >= VerbosityLevel);
            }   
        }

        private static IRestoreLogMessage ToRestoreLogMessage(ILogMessage message)
        {
            var restoreLogMessage = message as IRestoreLogMessage;

            if (restoreLogMessage == null)
            {
                restoreLogMessage = new RestoreLogMessage(message.Level, message.Code, message.Message);
            }

            return restoreLogMessage;
        }

        public IEnumerable<IRestoreLogMessage> Errors => _errors.ToArray();
    }
}
