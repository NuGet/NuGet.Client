// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Telemetry
{
    public class SearchSelectionTelemetryEvent : TelemetryEvent
    {
        public SearchSelectionTelemetryEvent(
            Guid parentId,
            int recommendedCount,
            int itemIndex,
            string packageId,
            NuGetVersion packageVersion,
            bool isPackageVulnerable,
            bool isPackageDeprecated,
            bool hasDeprecationAlternativePackage,
            ItemFilter currentTab,
            PackageLevel packageLevel,
            bool isSolutionLevel) : base("SearchSelection")
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            base["ParentId"] = parentId.ToString();
            base["RecommendedCount"] = recommendedCount;
            base["ItemIndex"] = itemIndex;
            base["IsPackageVulnerable"] = isPackageVulnerable;
            base["IsPackageDeprecated"] = isPackageDeprecated;
            base["HasDeprecationAlternativePackage"] = hasDeprecationAlternativePackage;
            base["Tab"] = currentTab;
            base["PackageLevel"] = packageLevel;
            base["IsSolutionLevel"] = isSolutionLevel;
            AddPiiData("PackageId", VSTelemetryServiceUtility.NormalizePackageId(packageId));
            AddPiiData("PackageVersion", VSTelemetryServiceUtility.NormalizePackageVersion(packageVersion));
        }
    }
}
