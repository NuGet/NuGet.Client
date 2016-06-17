﻿using System;
using System.Collections.Concurrent;
using NuGet.CommandLine.XPlat;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.XPlat.FuncTest
{
    public class TestCommandOutputLogger : CommandOutputLogger
    {
        private readonly bool _observeLogLevel;

        public TestLogger Logger { get; set; } = new TestLogger();

        public TestCommandOutputLogger(bool observeLogLevel = false)
            : base(LogLevel.Debug)
        {
            _observeLogLevel = observeLogLevel;
        }

        protected override void LogInternal(LogLevel logLevel, string message)
        {
            if (_observeLogLevel && logLevel < LogLevel)
            {
                return;
            }

            switch (logLevel)
            {
                case LogLevel.Debug:
                    Logger.LogDebug(message);
                    break;
                case LogLevel.Error:
                    Logger.LogError(message);
                    break;
                case LogLevel.Information:
                    Logger.LogInformation(message);
                    break;
                case LogLevel.Minimal:
                    Logger.LogMinimal(message);
                    break;
                case LogLevel.Verbose:
                    Logger.LogVerbose(message);
                    break;
                case LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                default:
                    Logger.LogDebug(message);
                    break;
            }
        }

        public ConcurrentQueue<string> Messages
        {
            get
            {
                return Logger.Messages;
            }
        }

        public ConcurrentQueue<string> ErrorMessages
        {
            get
            {
                return Logger.ErrorMessages;
            }
        }

        public ConcurrentQueue<string> VerboseMessages
        {
            get
            {
                return Logger.VerboseMessages;
            }
        }

        public int Errors
        {
            get
            {
                return Logger.Errors;
            }
        }

        public int Warnings
        {
            get
            {
                return Logger.Warnings;
            }
        }

        public string ShowMessages()
        {
            return string.Join(Environment.NewLine, Messages);
        }

        public string ShowErrors()
        {
            return string.Join(Environment.NewLine, ErrorMessages);
        }
    }
}
