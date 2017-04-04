// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Telemetry helper class to help emit granular level telemetry events for a specific nuget operation.
    /// It also provide helper methods to track operation time.
    /// </summary>
    public class TelemetryServiceHelper
    {
        public readonly ConcurrentDictionary<string, double> TelemetryEvents = 
            new ConcurrentDictionary<string, double>();

        private Stopwatch _stopWatch;

        /// <summary>
        /// If measuring detailed telemetry events is enabled then it will start or resume
        /// stopwatch timer for current event
        /// </summary>
        public void StartorResumeTimer()
        {
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

        /// <summary>
        /// Return current timer elapsed time.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Return current timer elapsed time in seconds.
        /// </summary>
        /// <returns></returns>
        public double GetTimerElapsedTimeInSeconds()
        {
            return GetTimerElapsedTime().TotalSeconds;
        }

        /// <summary>
        /// Emit a telemetry event with provided name and use current timer elapsed time.
        /// Make sure to start timer before calling this api.
        /// </summary>
        /// <param name="eventName">Telemetry event name for detailed step</param>
        public void AddTelemetryEvent(string eventName)
        {
            AddTelemetryEvent(eventName, GetTimerElapsedTimeInSeconds());
        }

        /// <summary>
        /// Emit telemetry event with provided event name and time duration.
        /// </summary>
        /// <param name="eventName">Telemetry event name</param>
        /// <param name="duration">Time duration</param>
        public void AddTelemetryEvent(string eventName, double duration)
        {
            TelemetryEvents.AddOrUpdate(eventName, duration,
                (key, existingVal) =>
                {
                    return existingVal + duration;
                });
        }
    }
}
