// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Keeps all counterfactuals state in one place
    /// </summary>
    internal class CounterfactualLogger
    {
        private int _emittedFlag = 0;

        internal CounterfactualLogger(string eventName)
        {
            EventName = eventName + "Counterfactual";
        }

        internal string EventName { get; }

        /// <summary>
        /// Emits counterfactual telemetry event once
        /// </summary>
        public void TryEmit()
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
        internal void Reset() => _emittedFlag = 0;

        internal static CounterfactualLogger TransitiveDependencies = new(nameof(TransitiveDependencies));
        internal static CounterfactualLogger PMUITransitiveDependencies = new(nameof(PMUITransitiveDependencies));
    }
}
