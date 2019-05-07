// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.Telemetry
{
    /// <summary>
    /// Represents that a search page was fetched. Similar to <see cref="TelemetryActivity"/> but does not depend on
    /// <see cref="IDisposable"/> and does not have a start and end time.
    /// </summary>
    public class SearchPageTelemetryEvent : TelemetryEvent
    {
        private StringBuilder _stringBuilder;

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

        private string ToJsonArray(IEnumerable<TimeSpan> sourceTimings)
        {
            var sb = Interlocked.Exchange(ref _stringBuilder, null);
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            sb.Append("[");
            foreach (var item in sourceTimings)
            {
                sb.Append(item.TotalSeconds);
            }
            if (sb[sb.Length - 1] == ',')
            {
                sb.Length--;
            }
            sb.Append("]");

            var result = sb.ToString();
            Interlocked.Exchange(ref _stringBuilder, sb);
            return result;
        }
    }
}
