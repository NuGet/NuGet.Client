// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Logging;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Logger to print formatted command output.
    /// </summary>
    internal class CommandOutputLogger : ILogger
    {
        private enum LogLevel
        {
            Debug = 0,
            Verbose = 1,
            Information = 2,
            Warning = 3,
            Error = 4
        }

        private static readonly bool _useConsoleColor = true;
        private Lazy<LogLevel> _verbosity;

        private LogLevel Verbosity { get { return _verbosity.Value; } }

        internal CommandOutputLogger(CommandOption verbosity)
        {
            _verbosity = new Lazy<LogLevel>(() =>
            {
                LogLevel level;
                if (!Enum.TryParse(verbosity.Value(), out level))
                {
                    level = LogLevel.Information;
                }
                return level;
            });
        }

        public void LogDebug(string data)
        {
            LogInternal(LogLevel.Debug, data);
        }

        public void LogError(string data)
        {
            LogInternal(LogLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            LogInternal(LogLevel.Information, data);
        }

        public void LogVerbose(string data)
        {
            LogInternal(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            LogInternal(LogLevel.Warning, data);
        }

        private void LogInternal(LogLevel logLevel, string message)
        {
            if(logLevel < Verbosity)
            {
                return;
            }

            var caption = string.Empty;
            if (_useConsoleColor)
            {
                switch (logLevel)
                {
                    case LogLevel.Debug:
                        caption = "debug";
                        break;
                    case LogLevel.Information:
                        caption = "info ";
                        break;
                    case LogLevel.Warning:
                        caption = "warn ";
                        break;
                    case LogLevel.Error:
                        caption = "error";
                        break;
                    case LogLevel.Verbose:
                        caption = "trace";
                        break;
                }
            }
            else
            {
                caption = logLevel.ToString().ToLowerInvariant();
            }
            Console.WriteLine($"{caption}: {message}");
        }
    }
}
