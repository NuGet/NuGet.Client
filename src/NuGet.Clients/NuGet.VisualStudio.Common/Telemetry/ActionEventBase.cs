// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Base class to generate telemetry data for nuget operations like install, update or restore.
    /// </summary>
    public abstract class ActionEventBase : INuGetTelemetryEvent
    {
        public ActionEventBase(
            string[] projectIds,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            DateTimeOffset endTime,
            double duration)
        {
            ProjectIds = projectIds;
            PackagesCount = packageCount;
            Status = status;
            StartTime = startTime;
            EndTime = endTime;
            Duration = duration;
            ProjectsCount = projectIds.Length;
        }

        public string[] ProjectIds { get; }

        public int PackagesCount { get; }

        public NuGetOperationStatus Status { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset EndTime { get; }

        public double Duration { get; }

        public int ProjectsCount { get; }

        public abstract TelemetryEvent ToTelemetryEvent(string operationId);
    }
}
