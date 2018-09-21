// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Telemetry event data for nuget operations like install, update, or uninstall.
    /// </summary>
    public class ActionsTelemetryEvent : ActionEventBase
    {
        public ActionsTelemetryEvent(
            string operationId,
            string[] projectIds,
            NuGetOperationType operationType,
            OperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            DateTimeOffset endTime,
            double duration) : base(operationId, projectIds, startTime, status, packageCount, endTime, duration)
        {
            OperationType = operationType;
            Source = source;
        }

        public NuGetOperationType OperationType { get; }

        public OperationSource Source { get; }
    }

    /// <summary>
    /// Define nuget operation type values.
    /// </summary>
    public enum NuGetOperationType
    {
        /// <summary>
        /// Install package action.
        /// </summary>
        Install = 0,

        /// <summary>
        /// Update package action.
        /// </summary>
        Update = 1,

        /// <summary>
        /// Uninstall package action.
        /// </summary>
        Uninstall = 2,
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
