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

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class ReportFrameworkPackage
    {
        private string FrameWork { get; set; }
        private List<TopLevelPackage> TopLevelPackages { get; set; }
        private List<TransitivePackage> TransitivePackages { get; set; }
    }

    internal class TopLevelPackage
    {
        private string PackageId { get; set; }
        private string AutoReference { get; set; }
        private string RequestedVersion { get; set; }
        private string ResolvedVersion { get; set; }
        private string LatestVersion { get; set; }
    }

    internal class TransitivePackage
    {
        private string PackageId { get; set; }
        private string ResolvedVersion { get; set; }
        private string LatestVersion { get; set; }
    }
}
