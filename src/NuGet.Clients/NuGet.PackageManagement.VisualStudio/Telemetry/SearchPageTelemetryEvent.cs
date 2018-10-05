// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public SearchPageTelemetryEvent(
            Guid parentId,
            int pageIndex,
            int resultCount,
            TimeSpan duration,
            LoadingStatus loadingStatus) : base("SearchPage")
        {
            base["ParentId"] = parentId.ToString();
            base["PageIndex"] = pageIndex;
            base["ResultCount"] = resultCount;
            base["Duration"] = duration.TotalSeconds;
            base["LoadingStatus"] = loadingStatus.ToString();
        }
    }
}
