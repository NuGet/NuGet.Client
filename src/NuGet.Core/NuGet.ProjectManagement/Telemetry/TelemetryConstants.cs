// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class contains telemetry events name and properties name.
    /// </summary>
    public static class TelemetryConstants
    {
        public static readonly string EventNamePrefix = "VS/NuGet/";
        public static readonly string PropertyNamePrefix = "VS.NuGet.";

        public static readonly string NuGetVersionPropertyName = PropertyNamePrefix + "NuGetVersion";
        public static readonly string ProjectIdPropertyName = PropertyNamePrefix + "ProjectId";

        // nuget telemetry event names
        public static readonly string ProjectInformationEventName = EventNamePrefix + "ProjectInformation";
        public static readonly string ProjectDependencyStatisticsEventName = EventNamePrefix + "DependencyStatistics";
        public static readonly string NugetActionEventName = EventNamePrefix + "NugetAction";
        public static readonly string NugetActionStepsEventName = EventNamePrefix + "NugetActionSteps";
        public static readonly string RestoreActionEventName = EventNamePrefix + "RestoreInformation";

        // project information event data
        public static readonly string NuGetProjectTypePropertyName = PropertyNamePrefix + "NuGetProjectType";

        // dependency statistics event data
        public static readonly string InstalledPackageCountPropertyName = PropertyNamePrefix + "InstalledPackageCount";

        // nuget action event data
        public static readonly string OperationIdPropertyName = PropertyNamePrefix + "OperationId";
        public static readonly string ProjectIdsPropertyName = PropertyNamePrefix + "ProjectIds";
        public static readonly string OperationTypePropertyName = PropertyNamePrefix + "OperationType";
        public static readonly string OperationSourcePropertyName = PropertyNamePrefix + "OperationSource";
        public static readonly string PackagesCountPropertyName = PropertyNamePrefix + "PackagesCount";
        public static readonly string OperationStatusPropertyName = PropertyNamePrefix + "OperationStatus";
        public static readonly string StartTimePropertyName = PropertyNamePrefix + "StartTime";
        public static readonly string EndTimePropertyName = PropertyNamePrefix + "EndTime";
        public static readonly string DurationPropertyName = PropertyNamePrefix + "Duration";

        // nuget action step event data
        public static readonly string StepNamePropertyName = PropertyNamePrefix + "StepName";
        public static readonly string PreviewBuildIntegratedStepName = "Preview build integrated action for project {0} time";
        public static readonly string GatherDependencyStepName = "Gather dependency action for project {0} time";
        public static readonly string ResolveDependencyStepName = "Resolve dependency action for project {0} time";
        public static readonly string ResolvedActionsStepName = "Resolved nuget actions for project {0} time";
        public static readonly string ExecuteActionStepName = "Executing nuget actions for project {0} time";

    }
}
