// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


/* Unmerged change from project 'NuGet.CommandLine.XPlat (netcoreapp3.1)'
Before:
using System.Collections.Generic;
After:
using System.Collections.Generic;
using NuGet;
using NuGet.CommandLine;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.ReportRenderers;
using NuGet.CommandLine.XPlat.ReportRenderers;
using NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderers;
*/
using System.Collections.Generic;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class ReportFrameworkPackage
    {
        internal string FrameWork { get; set; }
        internal List<TopLevelPackage> TopLevelPackages { get; set; }
        internal List<TransitivePackage> TransitivePackages { get; set; }
        public ReportFrameworkPackage(string frameWork, List<TopLevelPackage> topLevelPackages, List<TransitivePackage> transitivePackages)
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
