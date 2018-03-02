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
    public class ActionEventBase : TelemetryEvent
    {
        public ActionEventBase(
            string eventName,
            string operationId,
            string[] projectIds,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            DateTimeOffset endTime,
            double duration) :
            base(eventName, new Dictionary<string, object>
                {
                    { nameof(OperationId), operationId },
                    { nameof(ProjectIds), string.Join(",", projectIds) },
                    { nameof(PackagesCount), packageCount },
                    { nameof(Status), status },
                    { nameof(StartTime), startTime.ToString() },
                    { nameof(EndTime), endTime.ToString() },
                    { nameof(Duration), duration },
                    { nameof(ProjectsCount), projectIds.Length }
                })
        {
        }

        public string OperationId => (string)base[nameof(OperationId)];

        public string ProjectIds => (string)base[nameof(ProjectIds)];

        public int PackagesCount => (int)base[nameof(PackagesCount)];

        public NuGetOperationStatus Status => (NuGetOperationStatus)base[nameof(Status)];

        public string StartTime => (string)base[nameof(StartTime)];

        public string EndTime => (string)base[nameof(EndTime)];

        public double Duration => (double)base[nameof(Duration)];

        public int ProjectsCount => (int)base[nameof(ProjectsCount)];
    }
}
