// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderer
{
    internal class JsonOutputContent
    {
        internal int Version { get; set; } = JsonOutputFormat.Version;
        internal string Parameters { get; set; }
        internal List<ReportProblem> Problems { get; set; }
        internal List<string> Sources { get; set; }
        internal List<ReportProject> Projects { get; set; }
    }
}
