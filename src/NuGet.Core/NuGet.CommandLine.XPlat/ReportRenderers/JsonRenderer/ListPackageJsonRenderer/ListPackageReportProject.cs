// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer
{
    internal class ListPackageReportProject : ReportProject
    {
        internal string Path { get; set; }
        internal List<ListPackageReportFrameworkPackage> FrameworkPackages { get; set; }

        public ListPackageReportProject(string path, List<ListPackageReportFrameworkPackage> frameworkPackages)
        {
            Path = path;
            FrameworkPackages = frameworkPackages;
        }
    }
}
