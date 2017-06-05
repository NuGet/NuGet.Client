// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class Logger : ILogger, IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private LogLevel _logLevel;
        private IPlugin _plugin;
        private readonly ManualResetEventSlim _pluginSetEvent;
        private readonly BlockingCollection<LogData> _queue;

        internal Logger()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _queue = new BlockingCollection<LogData>();
            _pluginSetEvent = new ManualResetEventSlim();

            Task.Factory.StartNew(
                LogAsync,
                _cancellationTokenSource.Token,
                _cancellationTokenSource.Token,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            using (_queue)
            {
                _queue.CompleteAdding();

                try
                {
                    using (_cancellationTokenSource)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                }
                catch (Exception)
                {
                }
            }

            _isDisposed = true;

            GC.SuppressFinalize(this);
        }

        public void Log(LogLevel level, string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(level, data);
        }

        public void Log(ILogMessage message)
        {
            QueueLogRequestIfLogLevelIsCompatible(message.Level, message.Message);
        }

        public Task LogAsync(ILogMessage message)
        {
            QueueLogRequestIfLogLevelIsCompatible(message.Level, message.Message);

            return Task.CompletedTask;
        }

        public void LogDebug(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Debug, data);
        }

        public void LogError(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Error, data);
        }

        public void LogErrorSummary(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Information, data);
        }

        public void LogInformationSummary(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Information, data);
        }

        public void LogMinimal(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Minimal, data);
        }

        public void LogVerbose(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(LogLevel.Warning, data);
        }

        public Task LogAsync(LogLevel level, string data)
        {
            QueueLogRequestIfLogLevelIsCompatible(level, data);

            return Task.CompletedTask;
        }

        internal void SetLogLevel(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        internal void SetPlugin(IPlugin plugin)
        {
            Assert.IsNotNull(plugin, nameof(plugin));

            _plugin = plugin;
        }

        private async Task LogAsync(object state)
        {
            var cancellationToken = (CancellationToken)state;

            try
            {
                _pluginSetEvent.Wait(cancellationToken);

                foreach (var logData in _queue)
                {
                    await _plugin.Connection.SendRequestAndReceiveResponseAsync<LogRequest, LogResponse>(
                        MessageMethod.Log,
                        new LogRequest(logData.LogLevel, logData.Message),
                        CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
        }

        private void QueueLogRequestIfLogLevelIsCompatible(LogLevel logLevel, string data)
        {
            if (logLevel >= _logLevel)
            {
                _queue.Add(new LogData(logLevel, data));
            }
        }

        private sealed class LogData
        {
            internal LogLevel LogLevel { get; }
            internal string Message { get; }

            internal LogData(LogLevel logLevel, string message)
            {
                LogLevel = LogLevel;
                Message = message;
            }
        }
    }
}