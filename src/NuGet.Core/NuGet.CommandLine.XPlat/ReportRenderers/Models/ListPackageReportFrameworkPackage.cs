// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageReportFrameworkPackage
    {
        internal string Framework { get; set; }
        internal List<ListReportTopPackage> TopLevelPackages { get; set; }
        internal List<ListReportTransitivePackage> TransitivePackages { get; set; }
        public ListPackageReportFrameworkPackage(string frameWork)
        {
            Framework = frameWork;
        }
    }

    internal class ListReportPackage
    {
        internal string PackageId { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        public List<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }
        internal PackageDeprecationMetadata DeprecationReasons { get; set; }
        internal AlternatePackageMetadata AlternativePackage { get; set; }
    }

    internal class ListReportTopPackage : ListReportPackage
    {
        internal string OriginalRequestedVersion { get; set; }
        internal bool AutoReference { get; set; }
    }

    internal class ListReportTransitivePackage : ListReportPackage
    { }
}
