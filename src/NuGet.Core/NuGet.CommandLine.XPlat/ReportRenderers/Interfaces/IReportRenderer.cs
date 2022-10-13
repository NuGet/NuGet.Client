// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Interfaces
{
    internal interface IReportRenderer
    {
        void AddProblem(string errorText, ProblemType problemType);
        void End(ListPackageReportModel reportProject);
    }
}
