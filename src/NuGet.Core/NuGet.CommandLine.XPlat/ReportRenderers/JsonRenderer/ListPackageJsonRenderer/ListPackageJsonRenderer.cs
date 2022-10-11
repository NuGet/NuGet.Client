// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.Interfaces;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;
using NuGet.Packaging;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer
{
    internal abstract class ListPackageJsonRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected ListPackageReportModel _listPackageReportModel;
        protected ReportOutputVersion OutputVersion { get; private set; }
        internal string Parameters { get; private set; }

        protected ListPackageJsonRenderer(ReportOutputVersion outputVersion)
        {
            OutputVersion = outputVersion;
        }

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
            _problems.AddRange(_listPackageReportModel.Projects.Where(p => p.ProjectProblems != null).SelectMany(p => p.ProjectProblems));
            string jsonRenderedOutput = ListPackageJsonOutputSerializer.Render(new ListPackageJsonOutputContent()
            {
                ListPackageArgs = _listPackageReportModel.ListPackageArgs,
                Parameters = Parameters,
                Problems = _problems,
                Projects = _listPackageReportModel.Projects,
            });

            Console.WriteLine(jsonRenderedOutput);
        }

        public void SetParameters(string parametersText)
        {
            Parameters = parametersText;
        }
    }
}
