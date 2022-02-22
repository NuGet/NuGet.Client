// All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class PackageManagerLoadedEvent : TelemetryEvent
    {
        private const string EventName = "PMUILoaded";

        public PackageManagerLoadedEvent(
            Guid parentId,
            bool isSolutionLevel,
            RefreshOperationSource refreshSource,
            RefreshOperationStatus refreshStatus,
            string tab,
            bool isUIFiltering,
            TimeSpan duration) : base(EventName)
        {
            base["ParentId"] = parentId.ToString();
            base["IsSolutionLevel"] = isSolutionLevel;
            base["RefreshSource"] = refreshSource;
            base["RefreshStatus"] = refreshStatus;
            base["Tab"] = tab;
            base["IsUIFiltering"] = isUIFiltering;
            base["Duration"] = duration.TotalMilliseconds;
        }
    }
}
