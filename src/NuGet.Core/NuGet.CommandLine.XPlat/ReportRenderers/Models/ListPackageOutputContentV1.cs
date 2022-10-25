// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// List package json output version 1
    /// </summary>
    internal class ListPackageOutputContentV1
    {
        internal int ReportOutputVersion { get; set; } = 1;
        internal ListPackageArgs ListPackageArgs { get; set; }
        internal List<ReportProblem> Problems { get; set; }
        internal List<ListPackageProjectModel> Projects { get; set; }
    }
}
