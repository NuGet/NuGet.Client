// All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

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
            TimeSpan timeSinceLastRefresh) : base(EventName)
        {
            base["ParentId"] = parentId.ToString();
            base["IsSolutionLevel"] = isSolutionLevel;
            base["RefreshSource"] = refreshSource;
            base["RefreshStatus"] = refreshStatus;
            base["Tab"] = tab;
            base["TimeSinceLastRefresh"] = timeSinceLastRefresh.TotalMilliseconds;
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
    }
}
