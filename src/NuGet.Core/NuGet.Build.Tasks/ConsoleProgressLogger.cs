// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Build.Tasks
{
    internal class ConsoleProgressLogger : LoggerBase, IDisposable
    {
        private readonly ILogger _logger;
        private EtlListener _listener;
        private bool _disposedValue;
        private string _progressMessage;
        private object _lock = new object();

        public ConsoleProgressLogger(ILogger inner)
        {
            _logger = inner ?? throw new ArgumentNullException(nameof(inner));
            _listener = new EtlListener();
            _listener.MessageUpdated += MessageUpdated;
        }

        public override void Log(ILogMessage message)
        {
            if (_disposedValue)
            {
                return;
            }

            lock (_lock)
            {
                // Set cursor to start of line, let MSBuild log its message to console, then write the progress message on the new line.
                Console.SetCursorPosition(0, Console.CursorTop);
                _logger.Log(message);
                Console.Write(_progressMessage);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }

        private void MessageUpdated(object sender, string message)
        {
            if (_disposedValue) return;

            _progressMessage = message;
            lock (_lock)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(_progressMessage);
            }
        }

        private class EtlListener : EventListener
        {
            private uint _projectsToRestore;
            private uint _projectsComplete;

            public event EventHandler<string> MessageUpdated;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                //Console.WriteLine(eventSource.Name);
                if (eventSource.Name == "NuGet.Common")
                {
                    EnableEvents(eventSource, EventLevel.Verbose);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
#if NET5_0_OR_GREATER
                //string json = System.Text.Json.JsonSerializer.Serialize(eventData);
                //Console.WriteLine(json);
#endif

                if (eventData.EventName == "ProjectRestoreStart")
                {
                    _projectsToRestore++;
                    UpdateMessage();
                }
                else if (eventData.EventName == "ProjectRestoreStop")
                {
                    _projectsComplete++;
                    UpdateMessage();
                }
                else
                {
                    Console.WriteLine($"{eventData.EventSource.Name} {eventData.EventName}");
                }
            }

            private void UpdateMessage()
            {
                string message = $"Projects restored {_projectsComplete}/{_projectsToRestore}";
                MessageUpdated?.Invoke(this, message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _listener.MessageUpdated -= MessageUpdated;
                    _listener.Dispose();
                }

                _listener = null;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
