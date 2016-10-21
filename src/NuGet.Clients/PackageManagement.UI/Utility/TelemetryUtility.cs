// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Some utility apis for telemetry operations.
    /// </summary>
    public static class TelemetryUtility
    {
        private static Stopwatch _stopWatch;

        /// <summary>
        /// Create ActionTelemetryEvent instance.
        /// </summary>
        /// <param name="projects"></param>
        /// <param name="operationType"></param>
        /// <param name="source"></param>
        /// <param name="startTime"></param>
        /// <param name="status"></param>
        /// <param name="statusMessage"></param>
        /// <param name="packageCount"></param>
        /// <param name="endTime"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static ActionsTelemetryEvent GetActionTelemetryEvent(
            IEnumerable<NuGetProject> projects,
            NuGetOperationType operationType,
            OperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            double duration)
        {
            var sortedProjects = projects.OrderBy(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));

            var projectIds = sortedProjects.Select(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)).ToArray();

            return new ActionsTelemetryEvent(
                Guid.NewGuid().ToString(),
                projectIds,
                operationType,
                source,
                startTime,
                status,
                packageCount,
                DateTimeOffset.Now,
                duration);
        }

        public static void StartorResumeTimer()
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
            var duration = new TimeSpan();

            if (_stopWatch != null)
            {
                duration = _stopWatch.Elapsed;
                _stopWatch.Reset();
            }

            return duration;
        }

        public static double GetTimerElapsedTimeInSeconds()
        {
            return GetTimerElapsedTime().TotalSeconds;
        }
    }
}
