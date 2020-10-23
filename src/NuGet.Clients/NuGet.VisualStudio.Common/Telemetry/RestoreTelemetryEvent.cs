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
            int unknownProjectsCount,
            int projectJsonProjectsCount,
            int packageReferenceProjectsCount,
            int legacyPackageReferenceProjectsCount,
            int cpsPackageReferenceProjectsCount,
            int dotnetCliToolProjectsCount,
            int packagesConfigProjectsCount,
            DateTimeOffset endTime,
            double duration,
            bool isSolutionLoadRestore,
            IntervalTracker intervalTimingTracker) : base(RestoreActionEventName, operationId, projectIds, startTime, status, packageCount, endTime, duration)
        {
            base[nameof(OperationSource)] = source;
            base[nameof(NoOpProjectsCount)] = noOpProjectsCount;
            base[nameof(UpToDateProjectCount)] = upToDateProjectsCount;
            base[nameof(UnknownProjectsCount)] = unknownProjectsCount;
            base[nameof(ProjectJsonProjectsCount)] = projectJsonProjectsCount;
            base[nameof(PackageReferenceProjectsCount)] = packageReferenceProjectsCount;
            base[nameof(LegacyPackageReferenceProjectsCount)] = legacyPackageReferenceProjectsCount;
            base[nameof(CpsPackageReferenceProjectsCount)] = cpsPackageReferenceProjectsCount;
            base[nameof(DotnetCliToolProjectsCount)] = dotnetCliToolProjectsCount;
            base[nameof(PackagesConfigProjectsCount)] = packagesConfigProjectsCount;
            base[nameof(ForceRestore)] = forceRestore;
            base[nameof(IsSolutionLoadRestore)] = isSolutionLoadRestore;

            foreach (var (intervalName, intervalDuration) in intervalTimingTracker.GetIntervals())
            {
                base[intervalName] = intervalDuration;
            }
        }

        public const string RestoreActionEventName = "RestoreInformation";

        public RestoreOperationSource OperationSource => (RestoreOperationSource)base[nameof(OperationSource)];

        public int NoOpProjectsCount => (int)base[nameof(NoOpProjectsCount)];

        public bool ForceRestore => (bool)base[nameof(ForceRestore)];

        public bool IsSolutionLoadRestore => (bool)base[nameof(IsSolutionLoadRestore)];

        public int UpToDateProjectCount => (int)base[nameof(UpToDateProjectCount)];

        public int UnknownProjectsCount => (int)base[nameof(UnknownProjectsCount)];

        public int ProjectJsonProjectsCount => (int)base[nameof(ProjectJsonProjectsCount)];

        public int PackageReferenceProjectsCount => (int)base[nameof(PackageReferenceProjectsCount)];

        public int LegacyPackageReferenceProjectsCount => (int)base[nameof(LegacyPackageReferenceProjectsCount)];

        public int CpsPackageReferenceProjectsCount => (int)base[nameof(CpsPackageReferenceProjectsCount)];

        public int DotnetCliToolProjectsCount => (int)base[nameof(DotnetCliToolProjectsCount)];

        public int PackagesConfigProjectsCount => (int)base[nameof(PackagesConfigProjectsCount)];
    }
}
