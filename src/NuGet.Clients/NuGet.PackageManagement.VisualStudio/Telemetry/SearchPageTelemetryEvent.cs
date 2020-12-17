// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Telemetry
{
    /// <summary>
    /// Represents that a search page was fetched. Similar to <see cref="TelemetryActivity"/> but does not depend on
    /// <see cref="IDisposable"/> and does not have a start and end time.
    /// </summary>
    public class SearchPageTelemetryEvent : TelemetryEvent
    {
        public SearchPageTelemetryEvent(
            Guid parentId,
            int pageIndex,
            int resultCount,
            TimeSpan duration,
            IEnumerable<TimeSpan> sourceTimings,
            TimeSpan aggregationTime,
            LoadingStatus loadingStatus) : base("SearchPage")
        {
            base["ParentId"] = parentId.ToString();
            base["PageIndex"] = pageIndex;
            base["ResultCount"] = resultCount;
            base["Duration"] = duration.TotalSeconds;
            base["IndividualSourceDurations"] = ToJsonArray(sourceTimings);
            base["ResultsAggregationDuration"] = aggregationTime.TotalSeconds;
            base["LoadingStatus"] = loadingStatus.ToString();
        }

        private static string ToJsonArray(IEnumerable<TimeSpan> sourceTimings)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            foreach (var item in sourceTimings)
            {
                sb.Append(item.TotalSeconds);
                sb.Append(",");
            }
            if (sb[sb.Length - 1] == ',')
            {
                sb.Length--;
            }
            sb.Append("]");

            return sb.ToString();
        }
    }
}
