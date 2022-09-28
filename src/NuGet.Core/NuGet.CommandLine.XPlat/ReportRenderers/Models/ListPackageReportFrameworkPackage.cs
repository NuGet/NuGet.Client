// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Permissions;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageReportFrameworkPackage
    {
        internal string FrameWork { get; set; }
        internal bool AutoReference { get; set; }
        internal List<TopLevelPackage> TopLevelPackages { get; set; }
        internal List<TransitivePackage> TransitivePackages { get; set; }
        public ListPackageReportFrameworkPackage(string frameWork)
        {
            FrameWork = frameWork;
        }
    }

    internal class TopLevelPackage
    {
        internal string PackageId { get; set; }
        internal string RequestedVersion { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        public List<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }
        internal string DeprecationReasons { get; set; }
        internal string AlternativePackage { get; set; }
    }

    internal class TransitivePackage
    {
        internal string PackageId { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        public List<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }
        internal string DeprecationReasons { get; set; }
        internal string AlternativePackage { get; set; }
    }
}
