// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Build.Utilities;

namespace NuGet.Build.Tasks
{
    internal class ConsoleProgressLogger : IDisposable
    {
        private readonly TaskLoggingHelper _logger;
        private EtlListener _listener;
        private bool _disposedValue;

        public ConsoleProgressLogger(TaskLoggingHelper logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _listener = new EtlListener();
            _listener.MessageUpdated += MessageUpdated;
        }

        private void MessageUpdated(object sender, string message)
        {
            if (_disposedValue) return;

            _logger.LogProgress(message);
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
