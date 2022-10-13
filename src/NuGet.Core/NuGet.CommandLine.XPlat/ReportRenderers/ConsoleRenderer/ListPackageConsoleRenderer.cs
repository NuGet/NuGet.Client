// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.Interfaces;
using NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer
{
    internal class ListPackageConsoleRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        private ListPackageConsoleRenderer()
        { }

        public static ListPackageConsoleRenderer Instance { get; } = new ListPackageConsoleRenderer();

        public void AddProblem(string errorText, ProblemType problemType)
        {
            _problems.Add(new ReportProblem(string.Empty, errorText, problemType));
        }

        public void End(ListPackageReportModel listPackageReportModel)
        {
            ListPackageConsoleWriter.Render(new ListPackageOutputContent()
            {
                ListPackageArgs = listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = listPackageReportModel.Projects,
                AutoReferenceFound = listPackageReportModel.AutoReferenceFound
            });
        }
    }
}
