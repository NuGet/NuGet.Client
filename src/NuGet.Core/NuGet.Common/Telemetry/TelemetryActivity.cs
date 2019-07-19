// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Common
{
    public class TelemetryActivity : IDisposable
    {
        private DateTime _startTime;
        private Stopwatch _stopwatch;
        private Stopwatch _intervalWatch = new Stopwatch();
        private List<Tuple<string, TimeSpan>> _intervalList;

        public TelemetryEvent TelemetryEvent { get; set; }

        public Guid ParentId { get; }

        public Guid OperationId { get; }

        public static INuGetTelemetryService NuGetTelemetryService { get; set; }

        public TelemetryActivity(Guid parentId) :
            this(parentId, Guid.Empty, telemetryEvent: null)
        {
        }

        public TelemetryActivity(Guid parentId, Guid operationId) :
            this(parentId, operationId, telemetryEvent: null)
        {
        }

        public TelemetryActivity(Guid parentId, Guid operationId, TelemetryEvent telemetryEvent)
        {
            TelemetryEvent = telemetryEvent;
            ParentId = parentId;
            OperationId = operationId;

            _startTime = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
            _intervalList = new List<Tuple<string, TimeSpan>>();
        }

        public void StartIntervalMeasure()
        {
            _intervalWatch.Restart();
        }

        public void EndIntervalMeasure(string propertyName)
        {
            _intervalWatch.Stop();
            _intervalList.Add(new Tuple<string, TimeSpan>(propertyName, _intervalWatch.Elapsed));
        }

        public void Dispose()
        {
            _stopwatch.Stop();

            if (NuGetTelemetryService != null && TelemetryEvent != null)
            {
                var endTime = DateTime.UtcNow;
                TelemetryEvent["StartTime"] = _startTime.ToString("O");
                TelemetryEvent["EndTime"] = endTime.ToString("O");
                TelemetryEvent["Duration"] = _stopwatch.Elapsed.TotalSeconds;

                if (ParentId != Guid.Empty)
                {
                    TelemetryEvent[nameof(ParentId)] = ParentId.ToString();
                }

                if (OperationId != Guid.Empty)
                {
                    TelemetryEvent[nameof(OperationId)] = OperationId.ToString();
                }

                foreach (var interval in _intervalList)
                {
                    TelemetryEvent[interval.Item1] = interval.Item2.TotalSeconds;
                }

                NuGetTelemetryService.EmitTelemetryEvent(TelemetryEvent);
            }
        }

        public static void EmitTelemetryEvent(TelemetryEvent TelemetryEvent)
        {
            NuGetTelemetryService?.EmitTelemetryEvent(TelemetryEvent);
        }

        /// <summary>
        /// Creates a TelemetryActivity.
        /// </summary>
        /// <param name="parentId">OperationId of the parent event.</param>
        /// <param name="eventName">Name of the event.</param>
        /// <returns>TelemetryActivity with a given parentId and new operationId and a TelemetryEvent with eventName</returns>
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationIdAndEvent(Guid parentId, string eventName)
        {
            return new TelemetryActivity(parentId, Guid.NewGuid(), null)
            {
                TelemetryEvent = new TelemetryEvent(eventName)
            };
        }

        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId(Guid parentId)
        {
            return new TelemetryActivity(parentId, Guid.NewGuid(), null);
        }

        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId()
        {
            return new TelemetryActivity(Guid.Empty, Guid.NewGuid(), null);
        }
    }
}
