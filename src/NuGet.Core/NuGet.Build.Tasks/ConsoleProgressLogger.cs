// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Build.Utilities;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

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
            private uint[] _download;
            private uint _downloadSpeed;
            private int _downloadIndex;
            private int _downloadNow;
            private CancellationTokenSource _cancellationTokenSource;
            private Task _downloadUpdater;

            public EtlListener()
            {
                _download = new uint[2 * 5];
                _cancellationTokenSource = new CancellationTokenSource();
                _downloadUpdater = UpdateDownloadSpeed(_cancellationTokenSource.Token);
            }

            private async Task UpdateDownloadSpeed(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken).ConfigureAwait(false);

                        var downloaded = Interlocked.Exchange(ref _downloadNow, 0);

                        _downloadSpeed = _downloadSpeed - _download[_downloadIndex] + (uint)downloaded;
                        _download[_downloadIndex] = (uint)downloaded;
                        _downloadNow = 0;
                        _downloadIndex = (_downloadIndex + 1) % _download.Length;

                        UpdateMessage();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }

            public event EventHandler<string> MessageUpdated;

            public override void Dispose()
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();

                base.Dispose();
            }

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
                else if (eventData.EventName == "HttpDownload")
                {
                    var bytes = (uint)eventData.Payload[0];
                    Interlocked.Add(ref _downloadNow, (int)bytes);
                }
                else
                {
                    Console.WriteLine($"{eventData.EventSource.Name} {eventData.EventName}");
                }
            }

            private void UpdateMessage()
            {
                var bps = _downloadSpeed / 5;
                string message = $"Projects restored {_projectsComplete}/{_projectsToRestore}. HTTP downloaded speed = {bps}";
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
