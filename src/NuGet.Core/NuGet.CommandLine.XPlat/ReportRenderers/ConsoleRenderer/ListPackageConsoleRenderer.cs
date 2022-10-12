// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.Interfaces;
using NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer
{
    internal class ListPackageConsoleRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected ListPackageReportModel _listPackageReportModel;
        private ListPackageConsoleRenderer()
        { }

        public static ListPackageConsoleRenderer Instance { get; } = new ListPackageConsoleRenderer();

        public void AddProblem(string errorText, ProblemType problemType)
        {
            _problems.Add(new ReportProblem(string.Empty, errorText, problemType));
        }

        public void Write(ListPackageReportModel listPackageReportModel)
        {
            _listPackageReportModel = listPackageReportModel;
        }

        public void End()
        {
            ListPackageConsoleWriter.Render(new ListPackageOutputContent()
            {
                ListPackageArgs = _listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = _listPackageReportModel.Projects,
                AutoReferenceFound = _listPackageReportModel.AutoReferenceFound
            });
        }

        public void SetParameters(string parametersText)
        {
            //not needed for console
        }
    }
}
