// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.Telemetry
{
    public class ActionTelemetryStepEvent : INuGetTelemetryEvent
    {
        public ActionTelemetryStepEvent(string stepName, double duration)
        {
            StepName = stepName;
            Duration = duration;
        }

        public string StepName { get; }

        public double Duration { get; }

        public TelemetryEvent ToTelemetryEvent(string operationId)
        {
            return new TelemetryEvent(
                TelemetryConstants.NugetActionStepsEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.OperationIdPropertyName, operationId },
                    { nameof(StepName), string.Join(",", StepName) },
                    { nameof(Duration), Duration }
                }
            );
        }
    }
}
