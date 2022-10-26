// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Calculated project model data for a targetframework
    /// </summary>
    internal class ListPackageReportFrameworkPackage
    {
        internal string Framework { get; set; }
        internal List<ListReportPackage> TopLevelPackages { get; set; }
        internal List<ListReportPackage> TransitivePackages { get; set; }
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
        internal string RequestedVersion { get; set; } // not needed for transitive package
        internal bool AutoReference { get; set; } // not needed for transitive package
    }
}
