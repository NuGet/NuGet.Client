// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Logger to print formatted command output.
    /// </summary>
    public class CommandOutputLogger : ILogger
    {
        private static readonly bool _useConsoleColor = true;
        private LogLevel _logLevel;

        public CommandOutputLogger(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        public LogLevel LogLevel
        {
            get { return _logLevel; }
            set { _logLevel = value; }
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

        public void LogMinimal(string data)
        {
            LogInternal(LogLevel.Minimal, data);
        }

        public void LogVerbose(string data)
        {
            LogInternal(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            LogInternal(LogLevel.Warning, data);
        }

        public void LogInformationSummary(string data)
        {
            if (_logLevel <= LogLevel.Information)
            {
                Console.WriteLine(data);
            }
        }

        public void LogErrorSummary(string data)
        {
            Console.Error.WriteLine(data);
        }

        protected virtual void LogInternal(LogLevel logLevel, string message)
        {
            if (logLevel < _logLevel)
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
                    case LogLevel.Verbose:
                        caption = "trace";
                        break;
                    case LogLevel.Information:
                        caption = "info ";
                        break;
                    case LogLevel.Minimal:
                        caption = "log  ";
                        break;
                    case LogLevel.Warning:
                        caption = "warn ";
                        break;
                    case LogLevel.Error:
                        caption = "error";
                        break;
                }
            }
            else
            {
                caption = logLevel.ToString().ToLowerInvariant();
            }

            if (message.IndexOf('\n') >= 0)
            {
                Console.Write(PrefixAllLines(caption, message));
            }
            else
            {
                Console.WriteLine($"{caption}: {message}");
            }
        }

        private static string PrefixAllLines(string caption, string message)
        {
            // handle messages with multiple lines
            var builder = new StringBuilder();
            using (var reader = new StringReader(message))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    builder.Append(caption);
                    builder.Append(": ");
                    builder.AppendLine(line);
                }
            }

            return builder.ToString();
        }
    }
}
