// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.PackageManagement
{
    public class ActionTelemetryStepEvent : TelemetryEvent
    {
        public ActionTelemetryStepEvent(string operationId, string stepName, double duration) :
            base(NugetActionStepsEventName, new Dictionary<string, object>
                {
                    { nameof(OperationId), operationId },
                    { nameof(StepName), string.Join(",", stepName) },
                    { nameof(Duration), duration }
                })
        {
        }

        public const string NugetActionStepsEventName = "NugetActionSteps";

        public string StepName => (string)base[nameof(StepName)];
        public double Duration => (double)base[nameof(Duration)];
        public string OperationId => (string)base[nameof(OperationId)];
    }
}
