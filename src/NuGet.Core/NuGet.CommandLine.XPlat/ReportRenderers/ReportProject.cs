// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class ReportProject
    {
        internal string Path { get; set; }
        internal List<ReportFrameworkPackage> FrameworkPackages { get; set; }

        public ReportProject(string path, List<ReportFrameworkPackage> frameworkPackages)
        {
            Path = path;
            FrameworkPackages = frameworkPackages;
        }
    }
}
