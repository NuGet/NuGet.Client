// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Common
{
    /// <summary> Represents telemetry activity which spans a time interval. </summary>
    /// <remarks> Always dispose the activity at the end of interval covered. </remarks>
    public class TelemetryActivity : IDisposable
    {
        private readonly DateTime _startTime;
        private readonly Stopwatch _stopwatch;
        private readonly Stopwatch _intervalWatch = new Stopwatch();
        private readonly List<Tuple<string, TimeSpan>> _intervalList;
        private readonly IDisposable _telemetryActivity;
        private bool _disposed;

        /// <summary> Telemetry event which represents end of telemetry activity. </summary>
        public TelemetryEvent TelemetryEvent { get; set; }

        /// <summary> Parent activity ID. </summary>
        public Guid ParentId { get; }

        /// <summary> Operation ID. </summary>
        public Guid OperationId { get; }

        /// <summary> Singleton of NuGet telemetry service instance. </summary>
        public static INuGetTelemetryService NuGetTelemetryService { get; set; }

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public TelemetryActivity(Guid parentId) :
            this(parentId, Guid.Empty, telemetryEvent: null)
        {
        }

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public TelemetryActivity(Guid parentId, Guid operationId) :
            this(parentId, operationId, telemetryEvent: null)
        {
        }

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public TelemetryActivity(Guid parentId, Guid operationId, TelemetryEvent telemetryEvent) :
            this(parentId, telemetryEvent, operationId)
        {
        }

        private TelemetryActivity(Guid parentId, TelemetryEvent telemetryEvent, Guid operationId)
        {
            if (telemetryEvent != null)
            {
                _telemetryActivity = NuGetTelemetryService?.StartActivity(telemetryEvent.Name);
            }

            TelemetryEvent = telemetryEvent;
            ParentId = parentId;
            OperationId = operationId;

            _startTime = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
            _intervalList = new List<Tuple<string, TimeSpan>>();
        }

        /// <summary> Start interval measure. </summary>
        public void StartIntervalMeasure()
        {
            _intervalWatch.Restart();
        }

        /// <summary> End interval measure. </summary>
        /// <param name="propertyName"> Property name to represents the interval. </param>
        public void EndIntervalMeasure(string propertyName)
        {
            _intervalWatch.Stop();
            _intervalList.Add(new Tuple<string, TimeSpan>(propertyName, _intervalWatch.Elapsed));
        }

        /// <summary> Stops tracking the activity and emits a telemetry event. </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
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

                _telemetryActivity?.Dispose();
            }

            _disposed = true;
        }

        /// <summary> Emit a singular telemetry event. </summary>
        /// <param name="TelemetryEvent"> Telemetry event. </param>
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

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationIdAndEvent(Guid parentId, string eventName)
        {
            return Create(parentId, new TelemetryEvent(eventName));
        }

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId(Guid parentId)
        {
            return Create(parentId, default(TelemetryEvent));
        }

        /// <summary> Obsolete. Use one of static Create members. </summary>
        [Obsolete]
        public static TelemetryActivity CreateTelemetryActivityWithNewOperationId()
        {
            return Create(Guid.Empty, default(TelemetryEvent));
        }
    }
}
