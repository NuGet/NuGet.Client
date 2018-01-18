// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.PackageManagement
{
    public class ActionTelemetryStepEvent : INuGetTelemetryEvent
    {
        public ActionTelemetryStepEvent(string stepName, double duration)
        {
            StepName = stepName;
            Duration = duration;
        }

        public const string NugetActionStepsEventName = "NugetActionSteps";

        public string StepName { get; }

        public double Duration { get; }

        public TelemetryEvent ToTelemetryEvent(string operationIdPropertyName , string operationId)
        {
            return new TelemetryEvent(
                NugetActionStepsEventName,
                new Dictionary<string, object>
                {
                    { operationIdPropertyName, operationId },
                    { nameof(StepName), string.Join(",", StepName) },
                    { nameof(Duration), Duration }
                }
            );
        }
    }
}
