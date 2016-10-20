// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Common
{
    /// <summary>
    /// Telemetry helper class to help emit granular level telemetry events for a specific nuget operation.
    /// It also provide helper methods to track operation time.
    /// </summary>
    public class TelemetryServiceHelper
    {
        private bool _shouldMeasureEvents;

        private IDictionary<string, double> _telemetryEvents;

        private Stopwatch _stopWatch;

        public static TelemetryServiceHelper Instance = new TelemetryServiceHelper();

        private readonly object _lockObject = new object();

        private TelemetryServiceHelper() { }

        /// <summary>
        /// If measuring detailed telemetry events is enabled then it will start or resume
        /// stopwatch timer for current event
        /// </summary>
        public void StartorResumeTimer()
        {
            if (!_shouldMeasureEvents)
            {
                return;
            }

            if (_stopWatch == null)
            {
                _stopWatch = Stopwatch.StartNew();
            }
            else
            {
                _stopWatch.Start();
            }
        }

        /// <summary>
        /// If a event timer is running, then it will stop it.
        /// </summary>
        public void StopTimer()
        {
            _stopWatch?.Stop();
        }

        public TimeSpan GetTimerElapsedTime()
        {
            var duration = new TimeSpan();

            if (_stopWatch != null)
            {
                duration = _stopWatch.Elapsed;
                _stopWatch.Reset();
            }

            return duration;
        }

        public double GetTimerElapsedTimeInSeconds()
        {
            return GetTimerElapsedTime().TotalSeconds;
        }

        /// <summary>
        /// If measuing telemetry detailed events is enabled, then it will emit a telemetry event
        /// with provided name and use current timer elapsed time.
        /// Make sure to start timer before calling this api.
        /// </summary>
        /// <param name="eventName">Telemetry event name for detailed step</param>
        public void AddTelemetryEvent(string eventName)
        {
            if (!_shouldMeasureEvents)
            {
                return;
            }

            AddTelemetryEvent(eventName, GetTimerElapsedTimeInSeconds());
        }

        /// <summary>
        /// If measuing telemetry detailed events is enabled, then it will emit a telemetry event
        /// </summary>
        /// <param name="eventName">Telemetry event name</param>
        /// <param name="duration">Time duration</param>
        public void AddTelemetryEvent(string eventName, double duration)
        {
            if (!_shouldMeasureEvents || _telemetryEvents.ContainsKey(eventName))
            {
                return;
            }

            lock(_lockObject)
            {
                _telemetryEvents.Add(eventName, duration);
            }
        }

        /// <summary>
        /// This indicates that it can measure telemetry granular level events.
        /// </summary>
        public void EnableTelemetryEvents()
        {
            _shouldMeasureEvents = true;
            _telemetryEvents = new Dictionary<string, double>();
        }

        /// <summary>
        /// It returns all granular level telemetry events for current nuget operation if it was 
        /// enabled to measure through EnableTelemetryEvents().
        /// </summary>
        /// <returns>TelemetryEvents</returns>
        public IDictionary<string, double> GetTelemetryEvents()
        {
            _shouldMeasureEvents = false;

            var events = _telemetryEvents?.ToDictionary(entry => entry.Key,
                entry => entry.Value);
            _telemetryEvents?.Clear();

            return events;
        }
    }
}
