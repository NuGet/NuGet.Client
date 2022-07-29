// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderers
{
    internal class ReportProject
    {
        private string Path { get; set; }
        private List<FrameworkPackage> FrameworkPackages { get; set; }

        public ReportProject(string path, List<FrameworkPackage> frameworkPackages)
        {
            Path = path;
            FrameworkPackages = frameworkPackages;
        }
    }
}
