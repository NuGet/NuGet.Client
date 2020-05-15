// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Common
{
    public class TelemetryActivity : IDisposable
    {
        private readonly DateTime _startTime;
        private readonly Stopwatch _stopwatch;
        private readonly Stopwatch _intervalWatch = new Stopwatch();
        private readonly List<Tuple<string, TimeSpan>> _intervalList;

        public TelemetryEvent TelemetryEvent { get; set; }

        public Guid ParentId { get; }

        public Guid OperationId { get; }

        public static INuGetTelemetryService NuGetTelemetryService { get; set; }

        [Obsolete]
        public TelemetryActivity(Guid parentId) :
            this(parentId, Guid.Empty, telemetryEvent: null)
        {
        }

        [Obsolete]
        public TelemetryActivity(Guid parentId, Guid operationId) :
            this(parentId, operationId, telemetryEvent: null)
        {
        }

        [Obsolete]
        public TelemetryActivity(Guid parentId, Guid operationId, TelemetryEvent telemetryEvent) :
            this(parentId, telemetryEvent, operationId)
        {
        }

        private TelemetryActivity(Guid parentId, TelemetryEvent telemetryEvent, Guid operationId)
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

        /// <summary> Creates a TelemetryActivity. </summary>
        /// <param name="eventName"> Name of the event. </param>
        /// <returns> TelemetryActivity with an empty parentId, new operationId, and a TelemetryEvent with eventName. </returns>
        public static TelemetryActivity Create(string eventName)
        {
            return Create(Guid.Empty, new TelemetryEvent(eventName));
        }

        /// <summary> Creates a TelemetryActivity. </summary>
        /// <param name="telemetryEvent"> Telemetry event. </param>
        /// <returns> TelemetryActivity with an empty parentId, new operationId, and given TelemetryEvent. </returns>
        public static TelemetryActivity Create(TelemetryEvent telemetryEvent)
        {
            return Create(Guid.Empty, telemetryEvent);
        }

        /// <summary> Creates a TelemetryActivity. </summary>
        /// <param name="parentId"> OperationId of the parent event. </param>
        /// <param name="eventName"> Name of the event. </param>
        /// <returns> TelemetryActivity with a given parentId, new operationId, and a TelemetryEvent with eventName. </returns>
        public static TelemetryActivity Create(Guid parentId, string eventName)
        {
            return Create(parentId, new TelemetryEvent(eventName));
        }

        /// <summary> Creates a TelemetryActivity. </summary>
        /// <param name="parentId"> OperationId of the parent event. </param>
        /// <param name="telemetryEvent"> Telemetry event. </param>
        /// <returns> TelemetryActivity with a given parentId, new operationId, and given TelemetryEvent. </returns>
        public static TelemetryActivity Create(Guid parentId, TelemetryEvent telemetryEvent)
        {
            return new TelemetryActivity(parentId, telemetryEvent, Guid.NewGuid());
        }

        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationIdAndEvent(Guid parentId, string eventName)
        {
            return Create(parentId, new TelemetryEvent(eventName));
        }

        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId(Guid parentId)
        {
            return Create(parentId, default(TelemetryEvent));
        }

        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId()
        {
            return Create(Guid.Empty, default(TelemetryEvent));
        }
    }
}
