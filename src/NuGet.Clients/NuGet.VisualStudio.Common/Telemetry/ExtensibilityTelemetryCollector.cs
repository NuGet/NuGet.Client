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
        private IVsFrameworkCompatibilityCounters IVsFrameworkCompatibility { get; }

        public ExtensibilityTelemetryCollector()
        {
            _eventListener = new ExtensibilityEventListener(this);

            INuGetProjectService = new INuGetProjectServiceCounters();
            IVsFrameworkCompatibility = new IVsFrameworkCompatibilityCounters();
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

            // INuGetProjectService
            data[nameof(INuGetProjectService) + "." + nameof(INuGetProjectService.GetInstalledPackagesAsync)] = INuGetProjectService.GetInstalledPackagesAsync;

            // IVsFrameworkCompatibility
            data[nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetNetStandardFrameworks)] = IVsFrameworkCompatibility.GetNetStandardFrameworks;
            data[nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetFrameworksSupportingNetStandard)] = IVsFrameworkCompatibility.GetFrameworksSupportingNetStandard;
            data[nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetNearest)] = IVsFrameworkCompatibility.GetNearest;

            return data;
        }

        private class INuGetProjectServiceCounters
        {
            public int GetInstalledPackagesAsync;
        }

        private class IVsFrameworkCompatibilityCounters
        {
            public int GetNetStandardFrameworks;
            public int GetFrameworksSupportingNetStandard;
            public int GetNearest;
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
                        // INuGetProjectService
                        case "INuGetProjectService/GetInstalledPackagesAsync":
                            Interlocked.Increment(ref _collector.INuGetProjectService.GetInstalledPackagesAsync);
                            break;

                        // IVsFrameworkCompatibility
                        case "IVsFrameworkCompatibility/GetNetStandardFrameworks":
                            Interlocked.Increment(ref _collector.IVsFrameworkCompatibility.GetNetStandardFrameworks);
                            break;
                        case "IVsFrameworkCompatibility/GetFrameworksSupportingNetStandard":
                            Interlocked.Increment(ref _collector.IVsFrameworkCompatibility.GetFrameworksSupportingNetStandard);
                            break;
                        case "IVsFrameworkCompatibility/GetNearest":
                            Interlocked.Increment(ref _collector.IVsFrameworkCompatibility.GetNearest);
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
