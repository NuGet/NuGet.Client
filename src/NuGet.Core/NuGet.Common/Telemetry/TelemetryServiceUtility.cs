// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Common
{
    public static class TelemetryServiceUtility
    {
        private static Stopwatch _stopWatch;

        public static void StartOrResumeTimer()
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

        public static void StopTimer()
        {
            _stopWatch?.Stop();
        }

        public static TimeSpan GetTimerElapsedTime()
        {
            if (_stopWatch != null)
            {
                var duration = _stopWatch.Elapsed;
                _stopWatch.Reset();
                return duration;
            }

            return TimeSpan.MinValue;
        }

        public static double GetTimerElapsedTimeInSeconds()
        {
            return GetTimerElapsedTime().TotalSeconds;
        }
    }
}
