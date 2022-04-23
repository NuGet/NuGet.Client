// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Serialization;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class PackageManagerCloseEvent : TelemetryEvent
    {
        private const string EventName = "PMUIClose";

        public PackageManagerCloseEvent(
            Guid parentId,
            bool isSolutionLevel,
            string tab,
            PackageManagerInstalledTabData installedTabData) : base(EventName)
        {
            base["ParentId"] = parentId.ToString();
            base["IsSolutionLevel"] = isSolutionLevel;
            base["Tab"] = tab;

            string prefix = PackageManagerInstalledTabData.PropertyPrefix;
            base[prefix + nameof(installedTabData.TopLevelPackageSelectedCount)] = installedTabData.TopLevelPackageSelectedCount;
            base[prefix + nameof(installedTabData.TransitivePackageSelectedCount)] = installedTabData.TransitivePackageSelectedCount;
            base[prefix + nameof(installedTabData.TopLevelPackagesExpandedCount)] = installedTabData.TopLevelPackagesExpandedCount;
            base[prefix + nameof(installedTabData.TopLevelPackagesCollapsedCount)] = installedTabData.TopLevelPackagesCollapsedCount;
            base[prefix + nameof(installedTabData.TransitivePackagesExpandedCount)] = installedTabData.TransitivePackagesExpandedCount;
            base[prefix + nameof(installedTabData.TransitivePackagesCollapsedCount)] = installedTabData.TransitivePackagesCollapsedCount;
        }
    }

    public class PackageManagerInstalledTabData
    {
        public const string PropertyPrefix = "Installed.";

        public string TabName { get; }

        public int TopLevelPackageSelectedCount { get; set; }

        public int TransitivePackageSelectedCount { get; set; }

        public int TopLevelPackagesExpandedCount { get; set; }

        public int TopLevelPackagesCollapsedCount { get; set; }

        public int TransitivePackagesExpandedCount { get; set; }

        public int TransitivePackagesCollapsedCount { get; set; }
    }
}
