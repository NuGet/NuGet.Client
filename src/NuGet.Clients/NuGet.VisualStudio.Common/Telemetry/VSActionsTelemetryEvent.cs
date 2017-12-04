// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    public class VSActionsTelemetryEvent : ActionsTelemetryEvent
    {
        public VSActionsTelemetryEvent(
           string[] projectIds,
           NuGetOperationType operationType,
           OperationSource source,
           DateTimeOffset startTime,
           NuGetOperationStatus status,
           int packageCount,
           DateTimeOffset endTime,
           double duration) : base(projectIds, operationType, startTime, status, packageCount, endTime, duration)
        {
            Source = source;
        }

        public OperationSource Source { get; }

        public override TelemetryEvent ToTelemetryEvent(string operationId)
        {
            return new TelemetryEvent(
                TelemetryConstants.NugetActionEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.OperationIdPropertyName, operationId },
                    { nameof(ProjectIds), string.Join(",", ProjectIds) },
                    { nameof(OperationType), OperationType },
                    { nameof(Source), Source },
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

    /// <summary>
    /// Define different sources to trigger nuget action.
    /// </summary>
    public enum OperationSource
    {
        /// <summary>
        /// When nuget action is trigger from Package Management Console.
        /// </summary>
        PMC = 0,

        /// <summary>
        /// When nuget action is trigger from Nuget Manager UI.
        /// </summary>
        UI = 1,

        /// <summary>
        /// When nuget action is trigger from nuget public api.
        /// </summary>
        API = 2,
    }
}
