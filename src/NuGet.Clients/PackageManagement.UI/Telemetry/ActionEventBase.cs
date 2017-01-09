// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Base class to generate telemetry data for nuget operations like install, update or restore.
    /// </summary>
    public abstract class ActionEventBase
    {
        public ActionEventBase(
            string operationId,
            string[] projectIds,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            DateTimeOffset endTime,
            double duration)
        {
            OperationId = operationId;
            ProjectIds = projectIds;
            PackagesCount = packageCount;
            Status = status;
            StartTime = startTime;
            EndTime = endTime;
            Duration = duration;
        }

        public string OperationId { get; }

        public string[] ProjectIds { get; }

        public int PackagesCount { get; }

        public NuGetOperationStatus Status { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset EndTime { get; }

        public double Duration { get; }
    }
}
