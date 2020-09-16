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
        public const string RestoreOperationChecks = nameof(RestoreOperationChecks);
        public const string PackagesConfigRestore = nameof(PackagesConfigRestore);
        public const string SolutionDependencyGraphSpecCreation = nameof(SolutionDependencyGraphSpecCreation);
        public const string PackageReferenceRestoreDuration = nameof(PackageReferenceRestoreDuration);
        public const string SolutionUpToDateCheck = nameof(SolutionUpToDateCheck);
        private const string UpToDateProjectCount = nameof(UpToDateProjectCount);
        private const string PotentialSolutionLoadRestore = nameof(PotentialSolutionLoadRestore);

        public RestoreTelemetryEvent(
            string operationId,
            string[] projectIds,
            bool forceRestore,
            RestoreOperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            int noOpProjectsCount,
            int upToDateProjectsCount,
            DateTimeOffset endTime,
            double duration,
            bool isPotentialSolutionLoadRestore,
            IntervalTracker intervalTimingTracker) : base(RestoreActionEventName, operationId, projectIds, startTime, status, packageCount, endTime, duration)
        {
            base[nameof(OperationSource)] = source;
            base[nameof(NoOpProjectsCount)] = noOpProjectsCount;
            base[UpToDateProjectCount] = upToDateProjectsCount;
            base[nameof(ForceRestore)] = forceRestore;
            base[PotentialSolutionLoadRestore] = isPotentialSolutionLoadRestore;

            foreach (var (intervalName, intervalDuration) in intervalTimingTracker.GetIntervals())
            {
                base[intervalName] = intervalDuration;
            }
        }

        public const string RestoreActionEventName = "RestoreInformation";

        public RestoreOperationSource OperationSource => (RestoreOperationSource)base[nameof(OperationSource)];

        public int NoOpProjectsCount => (int)base[nameof(NoOpProjectsCount)];

        public bool ForceRestore => (bool)base[nameof(ForceRestore)];
    }
}
