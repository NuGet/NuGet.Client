// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands.PackageDownload
{
    internal sealed class PackageDownloadLogger : ILogger
    {
        private readonly ILogger _primary;
        private readonly Verbosity _verbosity;

        public PackageDownloadLogger(
            ILogger primary,
            Verbosity verbosity)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _verbosity = verbosity;
        }

        public void LogDebug(string data)
        {
            if (_verbosity == Verbosity.Detailed)
            {
                _primary.LogDebug(data);
            }
        }
        public void LogVerbose(string data)
        {
            if (_verbosity == Verbosity.Detailed)
            {
                _primary.LogVerbose(data);
            }
        }
        public void LogInformation(string data)
        {
            if (_verbosity == Verbosity.Detailed)
            {
                _primary.LogInformation(data);
            }
        }
        public void LogMinimal(string data)
        {
            if (_verbosity != Verbosity.Quiet)
            {
                _primary.LogMinimal(data);
            }
        }
        public void LogWarning(string data)
        {
            if (_verbosity != Verbosity.Quiet)
            {
                _primary.LogWarning(data);
            }
        }
        public void LogError(string data)
        {
            _primary.LogError(data);
        }

        public void LogInformationSummary(string data)
        {
            if (_verbosity == Verbosity.Detailed)
            {
                _primary.LogInformationSummary(data);
            }
        }

        public void Log(LogLevel level, string data)
        {
            if (_verbosity == Verbosity.Quiet)
            {
                if (level == LogLevel.Error)
                {
                    _primary.Log(level, data);
                }
            }
            else if (_verbosity == Verbosity.Normal)
            {
                if (level == LogLevel.Error
                    || level == LogLevel.Warning
                    || level == LogLevel.Minimal
                    || level == LogLevel.Information)
                {
                    _primary.Log(level, data);
                }
            }
            else
            {
                _primary.Log(level, data);
            }
        }
        public Task LogAsync(LogLevel level, string data)
        {
            if (_verbosity == Verbosity.Quiet)
            {
                if (level == LogLevel.Error)
                {
                    return _primary.LogAsync(level, data);
                }

                return Task.CompletedTask;
            }
            else if (_verbosity == Verbosity.Normal)
            {
                if (level == LogLevel.Error
                    || level == LogLevel.Warning
                    || level == LogLevel.Minimal
                    || level == LogLevel.Information)
                {
                    return _primary.LogAsync(level, data);
                }

                return Task.CompletedTask;
            }
            else
            {
                return _primary.LogAsync(level, data);
            }
        }

        public void Log(ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public Task LogAsync(ILogMessage message)
        {
            return LogAsync(message.Level, message.Message);
        }
    }
}

