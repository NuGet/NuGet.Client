// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.VisualStudio.Telemetry;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Nuget actions performance telemetry service class.
    /// </summary>
    public class ActionsTelemetryService : ActionsTelemetryBase
    {
        public static ActionsTelemetryService Instance =
            new ActionsTelemetryService(TelemetrySession.Instance);

        public ActionsTelemetryService(ITelemetrySession telemetrySession) : 
            base(telemetrySession)
        {
        }

        public void EmitActionEvent(ActionsTelemetryEvent actionTelemetryData, IReadOnlyDictionary<string, double> detailedEvents)
        {
            if (actionTelemetryData == null)
            {
                throw new ArgumentNullException(nameof(actionTelemetryData));
            }

            if (detailedEvents != null)
            {
                // emit granular level events for current operation
                foreach (var eventName in detailedEvents.Keys)
                {
                    EmitActionStepsEvent(actionTelemetryData.OperationId, eventName, detailedEvents[eventName]);
                }
            }

            var telemetryEvent = new TelemetryEvent(
                TelemetryConstants.NugetActionEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.OperationIdPropertyName, actionTelemetryData.OperationId },
                    { TelemetryConstants.ProjectIdsPropertyName, string.Join(",", actionTelemetryData.ProjectIds) },
                    { TelemetryConstants.OperationTypePropertyName, actionTelemetryData.OperationType },
                    { TelemetryConstants.OperationSourcePropertyName, actionTelemetryData.Source },
                    { TelemetryConstants.PackagesCountPropertyName, actionTelemetryData.PackagesCount },
                    { TelemetryConstants.OperationStatusPropertyName, actionTelemetryData.Status },
                    { TelemetryConstants.StartTimePropertyName, actionTelemetryData.StartTime.ToString() },
                    { TelemetryConstants.EndTimePropertyName, actionTelemetryData.EndTime.ToString() },
                    { TelemetryConstants.DurationPropertyName, actionTelemetryData.Duration },
                    { TelemetryConstants.ProjectsCountPropertyName, actionTelemetryData.ProjectsCount }
                }
            );
            _telemetrySession.PostEvent(telemetryEvent);

        }

    }
}
