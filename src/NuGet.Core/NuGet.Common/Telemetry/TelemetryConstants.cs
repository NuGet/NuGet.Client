// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary>
    /// This class contains telemetry events name and properties name.
    /// </summary>
    public static class TelemetryConstants
    {
        public static readonly string VSEventNamePrefix = "VS/NuGet/";
        public static readonly string VSPropertyNamePrefix = "VS.NuGet.";

        public static readonly string NuGetVersionPropertyName = "NuGetVersion";
        public static readonly string ProjectIdPropertyName = "ProjectId";
        public static readonly string OperationIdPropertyName = "OperationId";

        // nuget telemetry event names
        public static readonly string ProjectInformationEventName = "ProjectInformation";
        public static readonly string ProjectDependencyStatisticsEventName = "DependencyStatistics";
        public static readonly string NugetActionEventName = "NugetAction";
        public static readonly string NugetActionStepsEventName = "NugetActionSteps";
        public static readonly string RestoreActionEventName = "RestoreInformation";

        // project information event data
        public static readonly string NuGetProjectTypePropertyName = "NuGetProjectType";

        // dependency statistics event data
        public static readonly string InstalledPackageCountPropertyName = "InstalledPackageCount";

        // nuget action step event data
        public static readonly string StepNamePropertyName = "StepName";
        public static readonly string PreviewBuildIntegratedStepName = "Preview build integrated action for project {0} time";
        public static readonly string GatherDependencyStepName = "Gather dependency action for project {0} time";
        public static readonly string ResolveDependencyStepName = "Resolve dependency action for project {0} time";
        public static readonly string ResolvedActionsStepName = "Resolved nuget actions for project {0} time";
        public static readonly string ExecuteActionStepName = "Executing nuget actions for project {0} time";

    }
}
