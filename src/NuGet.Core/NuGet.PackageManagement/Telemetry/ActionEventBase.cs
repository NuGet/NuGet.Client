// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Base class to generate telemetry data for nuget operations like install, update or restore.
    /// </summary>
    public class ActionEventBase : INuGetTelemetryEvent
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

        public const string NugetActionEventName = "NugetAction";

        public string[] ProjectIds { get; }

        public int PackagesCount { get; }

        public NuGetOperationStatus Status { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset EndTime { get; }

        public double Duration { get; }

        public int ProjectsCount { get; }

        public virtual TelemetryEvent ToTelemetryEvent(string operationIdPropertyName, string operationId)
        {
            return new TelemetryEvent(
                NugetActionEventName,
                new Dictionary<string, object>
                {
                    { operationIdPropertyName, operationId },
                    { nameof(ProjectIds), string.Join(",", ProjectIds) },
                    { nameof(PackagesCount), PackagesCount },
                    { nameof(Status), Status },
                    { nameof(StartTime), StartTime.ToString() },
                    { nameof(EndTime), EndTime.ToString() },
                    { nameof(Duration), Duration },
                    { nameof(ProjectsCount), ProjectsCount }
                }
            );
        }
    }
}
