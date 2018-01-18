// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.PackageManagement;

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
            double duration) : base(RestoreActionEventName, projectIds, startTime, status, packageCount, endTime, duration)
        {
            OperationSource = source;
            NoOpProjectsCount = noOpProjectsCount;
        }

        public const string RestoreActionEventName = "RestoreInformation";

        public RestoreOperationSource OperationSource { get; }

        public int NoOpProjectsCount { get; }

        public override TelemetryEvent ToTelemetryEvent(string operationIdPropertyName, string operationId)
        {
            var telemtryEvent = base.ToTelemetryEvent(operationIdPropertyName, operationId);

            telemtryEvent.Properties.Add(nameof(OperationSource), OperationSource);
            telemtryEvent.Properties.Add(nameof(NoOpProjectsCount), NoOpProjectsCount);

            return telemtryEvent;
        }
    }
}
