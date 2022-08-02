// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer
{
    internal class ListPackageReportFrameworkPackage
    {
        internal string FrameWork { get; set; }
        internal List<TopLevelPackage> TopLevelPackages { get; set; }
        internal List<TransitivePackage> TransitivePackages { get; set; }
        public ListPackageReportFrameworkPackage(string frameWork, List<TopLevelPackage> topLevelPackages, List<TransitivePackage> transitivePackages)
        {
            FrameWork = frameWork;
            TopLevelPackages = topLevelPackages;
            TransitivePackages = transitivePackages;
        }
    }

    internal class TopLevelPackage
    {
        internal string PackageId { get; set; }
        internal string AutoReference { get; set; }
        internal string RequestedVersion { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        internal List<string> DeprecationReasons { get; set; }
        //public TopLevelPackage(string packageId)
        //{
        //    PackageId = packageId;
        //}
    }

    internal class TransitivePackage
    {
        internal string PackageId { get; set; }
        internal string ResolvedVersion { get; set; }
        internal string LatestVersion { get; set; }
        internal string DeprecationReasons { get; set; }
        internal AlternatePackageMetadata AlternativePackage { get; set; }
    }
}
