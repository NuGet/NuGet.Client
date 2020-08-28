// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    public class PackagingLogMessage : IPackLogMessage
    {
        public LogLevel Level { get; set; }
        public NuGetLogCode Code { get; set; }
        public string Message { get; set; }
        public DateTimeOffset Time { get; set; }
        public string ProjectPath { get; set; }
        public WarningLevel WarningLevel { get; set; } = WarningLevel.Severe; //setting default to Severe as 0 implies show no warnings
        public string FilePath { get; set; }
        public int StartLineNumber { get; set; }
        public int StartColumnNumber { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumnNumber { get; set; }

        /// <summary>
        /// Initializes a new instance of the PackLogMessage class
        /// </summary>
        /// <param name="logLevel">The log level</param>
        /// <param name="logCode">The NuGet log code</param>
        /// <param name="message">The log message</param>
        private PackagingLogMessage(LogLevel logLevel, NuGetLogCode logCode, string message)
        {
            Level = logLevel;
            Code = logCode;
            Message = message;
            Time = DateTimeOffset.UtcNow;
        }

        private PackagingLogMessage(LogLevel logLevel, string message)
        {
            Message = message;
            Code = NuGetLogCode.Undefined;
            Level = logLevel;
            Time = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Create an error log message.
        /// </summary>
        /// <param name="code">The logging code</param>
        /// <param name="message">The log message</param>
        public static PackagingLogMessage CreateError(string message, NuGetLogCode code)
        {
            return new PackagingLogMessage(LogLevel.Error, code, message);
        }

        public static PackagingLogMessage CreateWarning(string message, NuGetLogCode code)
        {
            return new PackagingLogMessage(LogLevel.Warning, code, message);
        }

        public static PackagingLogMessage CreateMessage(string message, LogLevel logLevel)
        {
            return new PackagingLogMessage(logLevel, message);
        }

        /// <summary>
        /// Get default LogCode based on the log level
        /// </summary>
        /// <param name="logLevel">The log level</param>
        private static NuGetLogCode GetDefaultLogCode(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Error:
                    return NuGetLogCode.NU5000;
                case LogLevel.Warning:
                    return NuGetLogCode.NU5500;
                default:
                    return NuGetLogCode.Undefined;
            }
        }
    }
}
