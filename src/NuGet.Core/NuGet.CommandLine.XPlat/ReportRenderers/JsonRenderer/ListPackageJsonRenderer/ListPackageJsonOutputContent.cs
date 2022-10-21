// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer
{
    internal class ListPackageJsonOutputContent
    {
        internal int Version { get; set; } = ListPackageJsonOutputSerializer.Version;
        internal string Parameters { get; set; }
        internal ListPackageArgs ListPackageArgs { get; set; }
        internal List<ReportProblem> Problems { get; set; }
        internal List<ListPackageProjectDetails> Projects { get; set; }
    }
}
