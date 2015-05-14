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
    public class CommandOutputLogger : ILogger
    {
        private const string Debug = nameof(Debug);
        private const string Error = nameof(Error);
        private const string Information = nameof(Information);
        private const string Verbose = nameof(Verbose);
        private const string Warning = nameof(Warning);

        private static readonly bool _useConsoleColor = true;

        public void LogDebug(string data)
        {
            LogInternal(Debug, data);
        }

        public void LogError(string data)
        {
            LogInternal(Error, data);
        }

        public void LogInformation(string data)
        {
            LogInternal(Information, data);
        }

        public void LogVerbose(string data)
        {
            LogInternal(Verbose, data);
        }

        public void LogWarning(string data)
        {
            LogInternal(Warning, data);
        }

        private void LogInternal(string logLevel, string message)
        {
            var caption = string.Empty;
            if (_useConsoleColor)
            {
                switch (logLevel)
                {
                    case Debug:
                        caption = "\x1b[35mdebug\x1b[39m";
                        break;
                    case Information:
                        caption = "\x1b[32minfo \x1b[39m";
                        break;
                    case Warning:
                        caption = "\x1b[33mwarn \x1b[39m";
                        break;
                    case Error:
                        caption = "\x1b[31merror\x1b[39m";
                        break;
                    case Verbose:
                        caption = "\x1b[35mtrace\x1b[39m";
                        break;
                }
            }
            else
            {
                caption = logLevel;
            }

            lock(Console.Out)
            {
                AnsiConsole.GetOutput(_useConsoleColor).WriteLine($"{caption}: {message}");
            }
        }
    }
}
