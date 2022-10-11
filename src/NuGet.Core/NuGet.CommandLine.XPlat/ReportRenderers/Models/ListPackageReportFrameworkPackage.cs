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

    internal class ListPackage
    {
        internal string PackageId { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        public List<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }
        internal PackageDeprecationMetadata DeprecationReasons { get; set; }
        internal AlternatePackageMetadata AlternativePackage { get; set; }
    }

    internal class TopLevelPackage : ListPackage
    {
        internal string RequestedVersion { get; set; }
    }

    internal class TransitivePackage : ListPackage
    { }
}
