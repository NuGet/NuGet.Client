// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Ensures a given telemetry event is emitted once. Useful for counterfactual logging.
    /// </summary>
    internal class TelemetryOnceEmitter
    {
        private int _emittedFlag = 0;

        internal TelemetryOnceEmitter(string eventName)
        {
            EventName = eventName;
        }

        internal string EventName { get; }

        /// <summary>
        /// Emits counterfactual telemetry event once
        /// </summary>
        public void EmitIfNeeded()
        {
            if (Interlocked.CompareExchange(ref _emittedFlag, 1, 0) == 0)
            {
                try
                {
                    TelemetryActivity.EmitTelemetryEvent(new TelemetryEvent(EventName));
                }
                catch
                {
                    _emittedFlag = 0;
                    throw; // caller should handle telemetry failure
                }
            }
        }

        /// <summary>
        /// For testing purposes only
        /// </summary>
        internal void Reset() => Interlocked.Exchange(ref _emittedFlag, 0);
    }
}
