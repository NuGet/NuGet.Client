// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    internal sealed class ExtensibilityTelemetryCollector : IDisposable
    {
        private ExtensibilityEventListener _eventListener;
        private INuGetProjectServiceCounters INuGetProjectService { get; }

        public ExtensibilityTelemetryCollector()
        {
            _eventListener = new ExtensibilityEventListener(this);

            INuGetProjectService = new INuGetProjectServiceCounters();
        }

        public void Dispose()
        {
            _eventListener?.Dispose();
            _eventListener = null;

            GC.SuppressFinalize(this);
        }

        public TelemetryEvent ToTelemetryEvent()
        {
            TelemetryEvent data = new("extensibility");

            data[nameof(INuGetProjectService) + "." + nameof(INuGetProjectService.GetInstalledPackagesAsync)] = INuGetProjectService.GetInstalledPackagesAsync;

            return data;
        }

        private class INuGetProjectServiceCounters
        {
            public int GetInstalledPackagesAsync;
        }

        private class ExtensibilityEventListener : EventListener
        {
            private ExtensibilityTelemetryCollector _collector;

            public ExtensibilityEventListener(ExtensibilityTelemetryCollector collector)
            {
                _collector = collector;
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "NuGet-VS-Extensibility")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways);
                }
                Debug.WriteLine(nameof(ExtensibilityEventListener) + " found " + eventSource.Name);
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.Opcode == EventOpcode.Start)
                {
                    switch (eventData.EventName)
                    {
                        case "INuGetProjectService/GetInstalledPackagesAsync":
                            Interlocked.Increment(ref _collector.INuGetProjectService.GetInstalledPackagesAsync);
                            break;

                        default:
                            Debug.Assert(false, "VS Extensibility API without counter");
                            break;
                    }
                }
            }
        }
    }
}
