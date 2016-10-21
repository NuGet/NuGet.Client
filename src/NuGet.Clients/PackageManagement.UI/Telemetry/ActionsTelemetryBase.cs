// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.VisualStudio.Facade.Telemetry;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Base service class for nuget actions telemetry services
    /// </summary>
    public abstract class ActionsTelemetryBase
    {
        protected readonly ITelemetrySession _telemetrySession;

        public ActionsTelemetryBase(ITelemetrySession telemetrySession)
        {
            if (telemetrySession == null)
            {
                throw new ArgumentNullException(nameof(telemetrySession));
            }

            _telemetrySession = telemetrySession;
        }

        public void EmitActionStepsEvent(string operationId, string stepName, double duration)
        {
            var telemetryEvent = new TelemetryEvent(
                TelemetryConstants.NugetActionStepsEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.OperationIdPropertyName, operationId },
                    { TelemetryConstants.StepNamePropertyName, stepName },
                    { TelemetryConstants.DurationPropertyName, duration }
                }
            );
            _telemetrySession.PostEvent(telemetryEvent);
        }
    }
}
