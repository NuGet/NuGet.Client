// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal class RemappedLevelLogger : ILogger
    {
        private readonly ILogger _logger;
        private readonly Mapping _mapping;

        public RemappedLevelLogger(ILogger inner, Mapping mapping)
        {
            _logger = inner;
            _mapping = mapping;
        }

        private LogLevel GetMappedLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => _mapping.Debug,
                LogLevel.Verbose => _mapping.Verbose,
                LogLevel.Information => _mapping.Information,
                LogLevel.Minimal => _mapping.Minimal,
                LogLevel.Warning => _mapping.Warning,
                LogLevel.Error => _mapping.Error,
                _ => throw new System.ArgumentOutOfRangeException(nameof(level), level, "Unknown log level")
            };
        }

        public void Log(LogLevel level, string data)
        {
            var mappedLevel = GetMappedLevel(level);
            _logger.Log(mappedLevel, data);
        }

        public void Log(ILogMessage message)
        {
            var mappedLevel = GetMappedLevel(message.Level);
            _logger.Log(mappedLevel, message.Message);
        }

        public Task LogAsync(LogLevel level, string data)
        {
            var mappedLevel = GetMappedLevel(level);
            return _logger.LogAsync(mappedLevel, data);
        }


        public Task LogAsync(ILogMessage message)
        {
            var mappedLevel = GetMappedLevel(message.Level);
            return _logger.LogAsync(mappedLevel, message.Message);
        }

        public void LogDebug(string data)
        {
            _logger.Log(_mapping.Debug, data);
        }

        public void LogError(string data)
        {
            _logger.Log(_mapping.Error, data);
        }

        public void LogInformation(string data)
        {
            _logger.Log(_mapping.Information, data);
        }

        public void LogInformationSummary(string data)
        {
            _logger.Log(_mapping.Information, data);
        }

        public void LogMinimal(string data)
        {
            _logger.Log(_mapping.Minimal, data);
        }

        public void LogVerbose(string data)
        {
            _logger.Log(_mapping.Verbose, data);
        }

        public void LogWarning(string data)
        {
            _logger.Log(_mapping.Warning, data);
        }

        internal record Mapping
        {
            public LogLevel Debug { get; init; } = LogLevel.Debug;
            public LogLevel Verbose { get; init; } = LogLevel.Verbose;
            public LogLevel Information { get; init; } = LogLevel.Information;
            public LogLevel Minimal { get; init; } = LogLevel.Minimal;
            public LogLevel Warning { get; init; } = LogLevel.Warning;
            public LogLevel Error { get; init; } = LogLevel.Error;
        }
    }
}
