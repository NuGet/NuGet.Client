// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents an <see cref="ILogger" /> that forwards logging messages to multiple loggers.
    /// </summary>
    internal class ForwardingLogger : LoggerBase
    {
        private ILogger[] _loggers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwardingLogger" /> class.
        /// </summary>
        /// <param name="loggers">An array of <see cref="ILogger" /> objects to forward logging messages to.</param>
        public ForwardingLogger(params ILogger[] loggers)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        /// <inheritdoc />
        public override void Log(ILogMessage message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                if (_loggers[i] != null)
                {
                    _loggers[i].Log(message);
                }
            }
        }

        /// <inheritdoc />
        public override async Task LogAsync(ILogMessage message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                if (_loggers[i] != null)
                {
                    await _loggers[i].LogAsync(message);
                }
            }
        }
    }
}
