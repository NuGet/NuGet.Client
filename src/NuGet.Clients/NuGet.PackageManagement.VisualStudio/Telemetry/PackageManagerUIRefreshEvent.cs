// All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class PackageManagerUIRefreshEvent : TelemetryEvent
    {
        private const string EventName = "PMUIRefresh";

        public PackageManagerUIRefreshEvent(
            Guid parentId,
            bool isSolutionLevel,
            RefreshOperationSource refreshSource,
            RefreshOperationStatus refreshStatus,
            string tab,
            bool isUIFiltering,
            TimeSpan timeSinceLastRefresh,
            double? duration,
            string projectId,
            NuGetProjectKind projectKind) : base(EventName)
        {
            base["ParentId"] = parentId.ToString();
            base["IsSolutionLevel"] = isSolutionLevel;
            base["RefreshSource"] = refreshSource;
            base["RefreshStatus"] = refreshStatus;
            base["Tab"] = tab;
            base["IsUIFiltering"] = isUIFiltering;
            base["TimeSinceLastRefresh"] = timeSinceLastRefresh.TotalMilliseconds;

            if (!isSolutionLevel)
            {
                base["ProjectKind"] = projectKind;
                base["ProjectId"] = projectId;
            }

            if (duration.HasValue)
            {
                base["Duration"] = duration;
            }
        }
    }

    public enum RefreshOperationSource
    {
        ActionsExecuted,
        CacheUpdated,
        CheckboxPrereleaseChanged,
        ClearSearch,
        ExecuteAction,
        FilterSelectionChanged,
        PackageManagerLoaded,
        PackageSourcesChanged,
        ProjectsChanged,
        RestartSearchCommand,
        SourceSelectionChanged,
        PackagesMissingStatusChanged,
    }

    public enum RefreshOperationStatus
    {
        Success,
        NotApplicable,
        NoOp,
        Failed,
    }
}
