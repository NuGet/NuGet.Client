// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Telemetry event data for nuget restore operation.
    /// </summary>
    public class RestoreTelemetryEvent : ActionEventBase
    {
        public RestoreTelemetryEvent(
            string[] projectIds,
            RestoreOperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            int noOpProjectsCount,
            DateTimeOffset endTime,
            double duration) : base(projectIds, startTime, status, packageCount, endTime, duration)
        {
            OperationSource = source;
            NoOpProjectsCount = noOpProjectsCount;
        }

        public RestoreOperationSource OperationSource { get; }

        public int NoOpProjectsCount { get; }

        public override TelemetryEvent ToTelemetryEvent(string operationId)
        {
            return new TelemetryEvent(
                TelemetryConstants.RestoreActionEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.OperationIdPropertyName, operationId },
                    { nameof(ProjectIds), string.Join(",", ProjectIds) },
                    { nameof(OperationSource), OperationSource },
                    { nameof(PackagesCount), PackagesCount },
                    { nameof(Status), Status },
                    { nameof(StartTime), StartTime.ToString() },
                    { nameof(EndTime), EndTime.ToString() },
                    { nameof(Duration), Duration },
                    { nameof(ProjectsCount), ProjectsCount },
                    { nameof(NoOpProjectsCount), NoOpProjectsCount }
                }
            );
        }
    }
}
