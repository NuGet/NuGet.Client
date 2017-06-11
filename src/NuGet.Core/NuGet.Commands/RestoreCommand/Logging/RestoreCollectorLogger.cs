// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Commands
{
    public class RestoreCollectorLogger : LoggerBase, ICollectorLogger
    {
        private readonly ILogger _innerLogger;
        private readonly ConcurrentQueue<IRestoreLogMessage> _errors;
        private readonly bool _hideWarningsAndErrors;

        public IEnumerable<IRestoreLogMessage> Errors => _errors.ToArray();

        public WarningPropertiesCollection WarningPropertiesCollection { get; set; }
        
        public string ProjectPath { get; set; }

        /// <summary>
        /// Initializes an instance of the <see cref="RestoreCollectorLogger"/>, while still
        /// delegating all log messages to the inner logger.
        /// </summary>
        /// <param name="innerLogger">The inner logger used to delegate the logging.</param>
        /// <param name="verbosity">Minimum verbosity below which no logs will be passed to the inner logger.</param>
        /// <param name="hideWarningsAndErrors">If this is true, then errors and warnings will not be passed to inner logger.</param>
        public RestoreCollectorLogger(ILogger innerLogger, LogLevel verbosity, bool hideWarningsAndErrors)
            : base(verbosity)
        {
            _innerLogger = innerLogger;
            _errors = new ConcurrentQueue<IRestoreLogMessage>();
            _hideWarningsAndErrors = hideWarningsAndErrors;
        }

        /// <summary>
        /// Initializes an instance of the <see cref="RestoreCollectorLogger"/>, while still
        /// delegating all log messages to the inner logger.
        /// </summary>
        /// <param name="innerLogger">The inner logger used to delegate the logging.</param>
        /// <param name="hideWarningsAndErrors">If this is false, then errors and warnings will not be passed to inner logger.</param>
        public RestoreCollectorLogger(ILogger innerLogger, bool hideWarningsAndErrors)
            : this(innerLogger, LogLevel.Debug, hideWarningsAndErrors)
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="RestoreCollectorLogger"/>, while still
        /// delegating all log messages to the inner logger.
        /// </summary>
        /// <param name="innerLogger">The inner logger used to delegate the logging.</param>
        /// <param name="verbosity">Minimum verbosity below which no logs will be passed to the inner logger.</param>
        public RestoreCollectorLogger(ILogger innerLogger, LogLevel verbosity)
            : this(innerLogger, verbosity, hideWarningsAndErrors: false)
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="RestoreCollectorLogger"/>, while still
        /// delegating all log messages to the inner logger.
        /// </summary>
        /// <param name="innerLogger">The inner logger used to delegate the logging.</param>
        public RestoreCollectorLogger(ILogger innerLogger)
            : this(innerLogger, LogLevel.Debug, hideWarningsAndErrors: false)
        {
        }

        public void Log(IRestoreLogMessage message)
        {
            // This will be true only when the Message is a Warning and should be suppressed.
            if (WarningPropertiesCollection == null || !WarningPropertiesCollection.ApplyWarningProperties(message))
            {
                if (string.IsNullOrEmpty(message.FilePath))
                {
                    message.FilePath = message.ProjectPath ?? ProjectPath;
                }

                if (CollectMessage(message.Level))
                {
                    _errors.Enqueue(message);
                }

                if (DisplayMessage(message))
                {
                    _innerLogger.Log(message);
                }
            }
        }

        public Task LogAsync(IRestoreLogMessage message)
        {

            // This will be true only when the Message is a Warning and should be suppressed.
            if (WarningPropertiesCollection == null || !WarningPropertiesCollection.ApplyWarningProperties(message))
            {
                if (string.IsNullOrEmpty(message.FilePath))
                {
                    message.FilePath = message.ProjectPath ?? ProjectPath;
                }

                if (CollectMessage(message.Level))
                {
                    _errors.Enqueue(message);
                }

                if (DisplayMessage(message))
                {
                    return _innerLogger.LogAsync(message);
                }
            }

            return Task.FromResult(0);
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
                return ((!_hideWarningsAndErrors || message.ShouldDisplay) && message.Level >= VerbosityLevel);
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
    }
}
