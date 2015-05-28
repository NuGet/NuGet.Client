// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Logging;

namespace NuGet.CommandLine
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
                        caption = "\x1b[35mdebug\x1b[39m";
                        break;
                    case LogLevel.Information:
                        caption = "\x1b[32minfo \x1b[39m";
                        break;
                    case LogLevel.Warning:
                        caption = "\x1b[33mwarn \x1b[39m";
                        break;
                    case LogLevel.Error:
                        caption = "\x1b[31merror\x1b[39m";
                        break;
                    case LogLevel.Verbose:
                        caption = "\x1b[35mtrace\x1b[39m";
                        break;
                }
            }
            else
            {
                caption = logLevel.ToString().ToLowerInvariant();
            }

            lock (Console.Out)
            {
                AnsiConsole.GetOutput(_useConsoleColor).WriteLine($"{caption}: {message}");
            }
        }
    }
}
