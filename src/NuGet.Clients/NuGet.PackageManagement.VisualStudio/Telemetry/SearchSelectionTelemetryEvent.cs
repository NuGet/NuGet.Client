// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.PackageManagement.Telemetry
{
    public class SearchSelectionTelemetryEvent : TelemetryEvent
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "We require lowercase package names in telemetry so that the hashes are consistent")]
        public SearchSelectionTelemetryEvent(
            Guid parentId,
            int recommendedCount,
            int itemIndex,
            string packageId,
            NuGetVersion packageVersion,
            bool isPackageVulnerable,
            bool isPackageDeprecated) : base("SearchSelection")
        {
            base["ParentId"] = parentId.ToString();
            base["RecommendedCount"] = recommendedCount;
            base["ItemIndex"] = itemIndex;
            base["IsPackageVulnerable"] = isPackageVulnerable;
            base["IsPackageDeprecated"] = isPackageDeprecated;
            AddPiiData("PackageId", packageId.ToLowerInvariant());
            AddPiiData("PackageVersion", packageVersion.ToNormalizedString().ToLowerInvariant());
        }
    }
}
